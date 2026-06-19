import { css, customElement, html, nothing, property, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbFormControlMixin } from "@umbraco-cms/backoffice/validation";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { UMB_NOTIFICATION_CONTEXT, type UmbNotificationContext } from "@umbraco-cms/backoffice/notification";
import { UMB_AUTH_CONTEXT, type UmbAuthContext } from "@umbraco-cms/backoffice/auth";
import type {
    UmbPropertyEditorConfigCollection,
    UmbPropertyEditorUiElement,
} from "@umbraco-cms/backoffice/property-editor";

interface OAuthCompleteMessage {
    type: "oauth-complete";
    success: boolean;
    credentialId?: string;
    error?: string;
}

interface OAuthProviderStatusResponse {
    isConfigured: boolean;
    setupDocsUrl?: string;
}

const elementName = "umb-automate-property-editor-ui-oauth";

/** Interval (ms) to check if the popup was closed externally (user closed the window). */
const POPUP_POLL_INTERVAL = 500;

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

    #popup: Window | null = null;
    #popupPollTimer?: ReturnType<typeof setInterval>;
    #boundMessageHandler = this.#onMessage.bind(this);
    #notificationContext?: UmbNotificationContext;
    #authContext?: UmbAuthContext;

    constructor() {
        super();
        this.consumeContext(UMB_NOTIFICATION_CONTEXT, (context) => {
            this.#notificationContext = context;
        });
        this.consumeContext(UMB_AUTH_CONTEXT, (context) => {
            this.#authContext = context;
            this.#checkProviderStatus();
        });
    }

    public set config(config: UmbPropertyEditorConfigCollection | undefined) {
        if (!config) return;
        this._provider = config.getValueByAlias<string>("provider") ?? "";
        this.#checkProviderStatus();
    }

    async #checkProviderStatus() {
        if (!this._provider || !this.#authContext) return;

        try {
            const { base, credentials, token } = this.#authContext.getOpenApiConfiguration();
            const response = await fetch(
                `${base}/umbraco/automate/oauth/status/${encodeURIComponent(this._provider)}`,
                { credentials, headers: { Authorization: `Bearer ${await token()}` } },
            );

            if (!response.ok) {
                this._isProviderConfigured = undefined;
                return;
            }

            const data = (await response.json()) as OAuthProviderStatusResponse;
            this._isProviderConfigured = data.isConfigured;
            this._setupDocsUrl = data.setupDocsUrl;
        } catch {
            // Network/parse failure — leave undefined rather than risk a false warning.
            this._isProviderConfigured = undefined;
        }
    }

    override connectedCallback() {
        super.connectedCallback();
        window.addEventListener("message", this.#boundMessageHandler);
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

        if (data.success && data.credentialId) {
            this.value = data.credentialId;
            this.dispatchEvent(new UmbChangeEvent());
            this.#notify("positive", `Connected to ${this._provider || "provider"}.`);
        } else {
            this.#notify("danger", data.error ?? "Authentication failed. Please try again.");
        }
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
            this.#notify("danger", "Could not open authentication window. Please allow popups for this site and try again.");
            return;
        }

        this.#startPopupPolling();
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

    #renderNotConfiguredWarning(providerLabel: string) {
        return html`
            <div class="not-configured-warning">
                <uui-icon name="icon-alert"></uui-icon>
                <span>
                    <span>
                        ${providerLabel} is not configured. Add a client ID and secret under
                        <code>Umbraco:Automate:Providers:${this._provider}</code> in appsettings.json
                        before authenticating.
                        ${this._setupDocsUrl
                            ? html`
                                  <a href=${this._setupDocsUrl} target="_blank" rel="noopener noreferrer">
                                      Get ${providerLabel} credentials
                                  </a>
                              `
                            : nothing}
                    </span>
                    <span class="secrets-tip">
                        Keep the client secret out of source control. Use environment variables,
                        user secrets, or a key vault to inject it at deployment time.
                    </span>
                </span>
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

        .not-configured-warning {
            display: flex;
            align-items: flex-start;
            gap: var(--uui-size-space-3);
            color: var(--uui-color-warning-standalone, var(--uui-color-warning));
        }

        .not-configured-warning > span {
            display: flex;
            flex-direction: column;
            gap: var(--uui-size-space-1);
        }

        .not-configured-warning uui-icon {
            flex-shrink: 0;
            margin-top: 2px;
        }

        .not-configured-warning code {
            font-size: 0.9em;
        }

        .not-configured-warning .secrets-tip {
            font-size: 0.85em;
            color: var(--uui-color-text-alt);
        }
    `;
}

export { UmbAutomatePropertyEditorUIOAuthElement as element };

declare global {
    interface HTMLElementTagNameMap {
        [elementName]: UmbAutomatePropertyEditorUIOAuthElement;
    }
}
