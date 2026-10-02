import { css, customElement, html, nothing, property, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbFormControlMixin } from "@umbraco-cms/backoffice/validation";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { UMB_NOTIFICATION_CONTEXT, type UmbNotificationContext } from "@umbraco-cms/backoffice/notification";
import { UMB_AUTH_CONTEXT, type UmbAuthContext } from "@umbraco-cms/backoffice/auth";
import {
    UMB_SUBMITTABLE_WORKSPACE_CONTEXT,
    type UmbSubmittableWorkspaceContext,
} from "@umbraco-cms/backoffice/workspace";
import type {
    UmbPropertyEditorConfigCollection,
    UmbPropertyEditorUiElement,
} from "@umbraco-cms/backoffice/property-editor";

interface OAuthCompleteMessage {
    type: "oauth-complete";
    success: boolean;
    /**
     * Short-lived token for the credential just stored. It is kept as this editor's value and the
     * server exchanges it for the credential id when the connection is saved.
     */
    credentialToken?: string;
    error?: string;
}

interface OAuthProviderStatusResponse {
    isConfigured: boolean;
    setupDocsUrl?: string;
}

const elementName = "umb-automate-property-editor-ui-oauth";

/** Interval (ms) to check if the popup was closed externally (user closed the window). */
const POPUP_POLL_INTERVAL = 500;

/**
 * Fragment key the OAuth callback uses when it redirects the tab back after the same-tab flow
 * (see OAuthReturnUrl.cs). A fragment is never sent to the server or in the Referer header.
 */
const REDIRECT_FRAGMENT_KEY = "automate-oauth";

/**
 * sessionStorage key (per provider) for the nonce the same-tab flow round-trips. sessionStorage is
 * per tab, so a crafted `#automate-oauth=...&credentialToken=...` link opened anywhere else cannot
 * match it — without this, a link could bind the user's connection to an attacker's account.
 */
const nonceStorageKey = (provider: string) => `umb-automate-oauth-nonce:${provider.toLowerCase()}`;

/**
 * How long (ms) to wait for the workspace to move from its "create" route to its "edit" route after
 * saving a new entity. UmbWorkspaceIsNewRedirectController does that on a 500ms timeout.
 */
const SAVE_REDIRECT_TIMEOUT = 3000;

/** Duck-typed so this editor keeps working in workspaces that don't track unpersisted changes. */
type WorkspaceWithChangeTracking = UmbSubmittableWorkspaceContext & { getHasUnpersistedChanges?: () => boolean };

@customElement(elementName)
export class UmbAutomatePropertyEditorUIOAuthElement
    extends UmbFormControlMixin<string | undefined, typeof UmbLitElement>(UmbLitElement, undefined)
    implements UmbPropertyEditorUiElement
{
    @property({ type: Boolean })
    public readonly = false;

    @state()
    private _provider = "";

    @state()
    private _authenticating = false;

    /** undefined while the status check is in flight (or hasn't started yet). */
    @state()
    private _isProviderConfigured: boolean | undefined;

    @state()
    private _setupDocsUrl: string | undefined;

    /** True after the browser blocked the popup — offers the same-tab fallback. */
    @state()
    private _popupBlocked = false;

    #popup: Window | null = null;
    #popupPollTimer?: ReturnType<typeof setInterval>;
    #boundMessageHandler = this.#onMessage.bind(this);
    #notificationContext?: UmbNotificationContext;
    #authContext?: UmbAuthContext;
    #workspaceContext?: WorkspaceWithChangeTracking;
    /** The provider whose status has been requested, to dedupe the two triggers (config setter + auth context). */
    #checkedProvider?: string;

    constructor() {
        super();
        this.consumeContext(UMB_NOTIFICATION_CONTEXT, (context) => {
            this.#notificationContext = context;
        });
        this.consumeContext(UMB_AUTH_CONTEXT, (context) => {
            this.#authContext = context;
            this.#checkProviderStatus();
        });
        this.consumeContext(UMB_SUBMITTABLE_WORKSPACE_CONTEXT, (context) => {
            this.#workspaceContext = context as WorkspaceWithChangeTracking | undefined;
        });
    }

    public set config(config: UmbPropertyEditorConfigCollection | undefined) {
        if (!config) return;
        this._provider = config.getValueByAlias<string>("provider") ?? "";
        this.#checkProviderStatus();
        this.#consumeRedirectResult();
    }

    async #checkProviderStatus() {
        if (!this._provider || !this.#authContext) return;

        // Both the config setter and the auth-context callback call this, and the config setter can
        // fire repeatedly — only check each provider once.
        if (this._provider === this.#checkedProvider) return;
        const provider = this._provider;
        this.#checkedProvider = provider;

        try {
            const { base, credentials, token } = this.#authContext.getOpenApiConfiguration();
            const response = await fetch(
                `${base}/umbraco/automate/oauth/status/${encodeURIComponent(provider)}`,
                { credentials, headers: { Authorization: `Bearer ${await token()}` } },
            );

            if (!response.ok) {
                // Allow a retry, and keep any previously-known state rather than resetting to
                // undefined — a transient failure shouldn't clear a valid warning.
                this.#checkedProvider = undefined;
                return;
            }

            const data = (await response.json()) as OAuthProviderStatusResponse;
            this._isProviderConfigured = data.isConfigured;
            this._setupDocsUrl = data.setupDocsUrl;
        } catch {
            // Network/parse failure — allow a retry and leave the last-known state untouched
            // rather than risk clearing a valid warning.
            this.#checkedProvider = undefined;
        }
    }

    override connectedCallback() {
        super.connectedCallback();
        window.addEventListener("message", this.#boundMessageHandler);
        this.#consumeRedirectResult();
    }

    override disconnectedCallback() {
        super.disconnectedCallback();
        window.removeEventListener("message", this.#boundMessageHandler);
        this.#cleanup();
    }

    #notify(color: "danger" | "warning" | "positive", message: string) {
        this.#notificationContext?.peek(color, { data: { message } });
    }

    #onMessage(event: MessageEvent) {
        if (event.origin !== window.location.origin) return;

        const data = event.data as OAuthCompleteMessage;
        if (data?.type !== "oauth-complete") return;

        this.#cleanup();
        this._authenticating = false;

        if (data.success && data.credentialToken) {
            this.#applyCredential(data.credentialToken);
            this.#notify("positive", `Connected to ${this._provider || "provider"}. Save to keep the new authentication.`);
        } else {
            this.#notify("danger", data.error ?? "Authentication failed. Please try again.");
        }
    }

    #applyCredential(credentialToken: string) {
        this._popupBlocked = false;
        this.value = credentialToken;
        this.dispatchEvent(new UmbChangeEvent());
    }

    /**
     * Picks up the result of the same-tab flow: the OAuth callback redirects back to the workspace
     * URL with `#automate-oauth=1&provider=...&credentialToken=...` (or `&error=...`). Called from both
     * the config setter and connectedCallback because either may run last. It needs the provider so
     * that, with several OAuth editors on one page, only the matching one claims the result.
     */
    #consumeRedirectResult() {
        if (!this.isConnected || !this._provider) return;

        const hash = window.location.hash;
        if (!hash.includes(`${REDIRECT_FRAGMENT_KEY}=`)) return;

        const params = new URLSearchParams(hash.slice(1));
        if (params.get(REDIRECT_FRAGMENT_KEY) !== "1") return;
        if (params.get("provider")?.toLowerCase() !== this._provider.toLowerCase()) return;

        // Strip the fragment first so a re-render or reload cannot apply the result twice.
        history.replaceState(history.state, "", window.location.pathname + window.location.search);

        // Only accept a result this tab asked for (see nonceStorageKey). Fail closed if storage is
        // unavailable — the popup flow still works.
        let expectedNonce: string | null = null;
        try {
            const key = nonceStorageKey(this._provider);
            expectedNonce = sessionStorage.getItem(key);
            sessionStorage.removeItem(key);
        } catch {
            expectedNonce = null;
        }
        if (!expectedNonce || params.get("nonce") !== expectedNonce) return;

        const credentialToken = params.get("credentialToken");
        if (credentialToken) {
            this.#applyCredential(credentialToken);
            this.#notify("positive", `Connected to ${this._provider}. Save to keep the new authentication.`);
            return;
        }

        this.#notify("danger", params.get("error") ?? "Authentication failed. Please try again.");
    }

    #cleanup() {
        if (this.#popupPollTimer) {
            clearInterval(this.#popupPollTimer);
            this.#popupPollTimer = undefined;
        }
        if (this.#popup && !this.#popup.closed) {
            this.#popup.close();
        }
        this.#popup = null;
    }

    #startPopupPolling() {
        this.#popupPollTimer = setInterval(() => {
            if (this.#popup && this.#popup.closed) {
                this.#cleanup();
                if (this._authenticating) {
                    this._authenticating = false;
                    this.#notify("warning", "Authentication was cancelled.");
                }
            }
        }, POPUP_POLL_INTERVAL);
    }

    #onAuthenticate() {
        if (!this._provider) {
            this.#notify("danger", "No OAuth provider configured for this connection type.");
            return;
        }

        if (this._isProviderConfigured === false) {
            this.#notify("danger", `${this._provider} is not configured. Add a client ID and secret in appsettings.json first.`);
            return;
        }

        this._authenticating = true;

        const url = `/umbraco/automate/oauth/challenge/${encodeURIComponent(this._provider)}`;
        const width = 600;
        const height = 700;
        const left = window.screenX + (window.outerWidth - width) / 2;
        const top = window.screenY + (window.outerHeight - height) / 2;

        try {
            this.#popup = window.open(
                url,
                "oauth-popup",
                `width=${width},height=${height},left=${left},top=${top},popup=yes`,
            );
        } catch {
            this.#popup = null;
        }

        if (!this.#popup) {
            this._authenticating = false;
            // Don't navigate away on our own — the user may have unsaved changes. Offer the same-tab
            // fallback and let them choose it.
            this._popupBlocked = true;
            return;
        }

        this._popupBlocked = false;
        this.#startPopupPolling();
    }

    /**
     * Same-tab fallback for a blocked popup: navigates this tab to the challenge endpoint with a
     * return URL, and the callback redirects back here with the result in the fragment.
     *
     * The workspace is saved first — leaving the page would otherwise discard unsaved edits, and for
     * a new entity the "create" URL would come back to a fresh scaffold (and a duplicate on the next
     * save). So: save, wait for a new entity's route to switch to "edit", then navigate.
     */
    async #onContinueInTab() {
        const workspace = this.#workspaceContext;
        const wasNew = workspace?.getIsNew() === true;

        if (workspace && (wasNew || workspace.getHasUnpersistedChanges?.())) {
            const pathBeforeSave = window.location.pathname;
            this._authenticating = true;
            try {
                await workspace.requestSubmit();
            } catch {
                // Validation or save failure — the workspace shows its own messages; checked below.
            }

            if (workspace.getIsNew() !== false || workspace.getHasUnpersistedChanges?.()) {
                this._authenticating = false;
                this.#notify("danger", "Save your changes before continuing, then try again.");
                return;
            }

            if (wasNew && !(await this.#waitForPathChange(pathBeforeSave))) {
                this._authenticating = false;
                this.#notify("warning", 'Saved. Select "Continue in this tab" again to authenticate.');
                return;
            }
        }

        let nonce: string;
        try {
            // randomUUID needs a secure context; sessionStorage can throw when site data is blocked.
            nonce = crypto.randomUUID();
            sessionStorage.setItem(nonceStorageKey(this._provider), nonce);
        } catch {
            this._authenticating = false;
            this.#notify(
                "danger",
                "Authentication cannot continue in this tab in this browser. Allow popups for this site and try again.",
            );
            return;
        }

        this._authenticating = true;
        const returnUrl = window.location.pathname + window.location.search;
        window.location.assign(
            `/umbraco/automate/oauth/challenge/${encodeURIComponent(this._provider)}` +
                `?returnUrl=${encodeURIComponent(returnUrl)}&nonce=${encodeURIComponent(nonce)}`,
        );
    }

    #waitForPathChange(from: string): Promise<boolean> {
        return new Promise((resolve) => {
            const started = Date.now();
            const timer = setInterval(() => {
                if (window.location.pathname !== from) {
                    clearInterval(timer);
                    resolve(true);
                } else if (Date.now() - started > SAVE_REDIRECT_TIMEOUT) {
                    clearInterval(timer);
                    resolve(false);
                }
            }, 100);
        });
    }

    #onDisconnect() {
        this.value = undefined;
        this.dispatchEvent(new UmbChangeEvent());
    }

    override render() {
        if (this.value) {
            return this.#renderConnected();
        }
        return this.#renderDisconnected();
    }

    #renderConnected() {
        return html`
            <div class="oauth-state connected">
                <uui-icon name="icon-check"></uui-icon>
                <span class="label">Connected</span>
                ${!this.readonly
                    ? html`
                          <uui-button
                              look="secondary"
                              label="Disconnect"
                              @click=${this.#onDisconnect}
                          >
                              Disconnect
                          </uui-button>
                      `
                    : nothing}
            </div>
        `;
    }

    #renderDisconnected() {
        const providerLabel = this._provider || "provider";
        const isUnconfigured = this._isProviderConfigured === false;

        return html`
            <div class="oauth-state disconnected">
                ${isUnconfigured ? this.#renderNotConfiguredWarning(providerLabel) : nothing}
                ${this._popupBlocked && !isUnconfigured ? this.#renderPopupBlocked(providerLabel) : nothing}
                <uui-button
                    look="primary"
                    label=${`Authenticate with ${providerLabel}`}
                    ?disabled=${this.readonly || this._authenticating || isUnconfigured}
                    @click=${this.#onAuthenticate}
                >
                    ${this._authenticating
                        ? html`<uui-loader-bar></uui-loader-bar>`
                        : html`Authenticate with ${providerLabel}`}
                </uui-button>
            </div>
        `;
    }

    #renderPopupBlocked(providerLabel: string) {
        const workspace = this.#workspaceContext;
        const needsSave = workspace?.getIsNew() === true || workspace?.getHasUnpersistedChanges?.() === true;
        const buttonLabel = needsSave ? "Save and continue in this tab" : "Continue in this tab";

        return html`
            <div class="popup-blocked-warning">
                <div class="heading">
                    <uui-icon name="icon-alert"></uui-icon>
                    <strong>Your browser blocked the ${providerLabel} sign-in window</strong>
                </div>
                <p>
                    Allow popups for this site and try again, or continue in this tab. You will come back
                    here after signing in to ${providerLabel}.
                </p>
                <uui-button
                    look="primary"
                    color="warning"
                    compact
                    label=${buttonLabel}
                    ?disabled=${this.readonly || this._authenticating}
                    @click=${this.#onContinueInTab}
                >
                    ${buttonLabel}
                </uui-button>
            </div>
        `;
    }

    #renderNotConfiguredWarning(providerLabel: string) {
        return html`
            <div class="not-configured-warning">
                <div class="heading">
                    <uui-icon name="icon-alert"></uui-icon>
                    <strong>${providerLabel} is not configured</strong>
                </div>
                <p>
                    Add a client ID and secret under
                    <code>Umbraco:Automate:Providers:${this._provider}</code> in appsettings.json
                    before authenticating.
                    <br/>
                    <small>

                        Keep the client secret out of source control. Use environment variables,
                        user secrets, or a key vault to inject it at deployment time.
                    </small>
                </p>
                ${this._setupDocsUrl
                    ? html`
                          <uui-button
                              look="secondary"
                              color="warning"
                              href=${this._setupDocsUrl}
                              target="_blank"
                              rel="noopener noreferrer"
                              label=${`Get ${providerLabel} credentials`}
                          >
                              Get ${providerLabel} credentials
                              <uui-icon name="icon-out"></uui-icon>
                          </uui-button>
                      `
                    : nothing}
            </div>
        `;
    }

    static override styles = css`
        :host {
            display: block;
        }

        .oauth-state {
            display: flex;
            align-items: center;
            gap: var(--uui-size-space-3);
        }

        .connected {
            color: var(--uui-color-positive);
        }

        .connected .label {
            font-weight: 600;
        }

        .disconnected {
            flex-direction: column;
            align-items: flex-start;
        }

        .not-configured-warning,
        .popup-blocked-warning {
            padding: var(--uui-size-space-4) var(--uui-size-space-5);
            border-radius: var(--uui-border-radius);
            border: 1px solid var(--uui-color-warning-standalone);
            background-color: var(--uui-color-warning);
            color: var(--uui-color-warning-contrast);
        }

        .not-configured-warning p,
        .popup-blocked-warning p {
            margin-top: 0;
        }

        .not-configured-warning code {
            font-size: 0.9em;
        }
    `;
}

export { UmbAutomatePropertyEditorUIOAuthElement as element };

declare global {
    interface HTMLElementTagNameMap {
        [elementName]: UmbAutomatePropertyEditorUIOAuthElement;
    }
}
