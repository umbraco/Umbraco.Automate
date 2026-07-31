import { css, html, customElement, property, state, nothing } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";
import type { UmbCodeEditorElement } from "@umbraco-cms/backoffice/code-editor";
import { AutomationsService } from "../../../api/sdk.gen.js";
import { UA_EMPTY_GUID, dispatchActionEvent } from "../../../core/index.js";
import { UaAutomationRunsChangedEvent } from "../../events/automation-runs-changed.event.js";

import "@umbraco-cms/backoffice/code-editor";
import "../../../core/components/webhook-url-field/webhook-url-field.element.js";

/** Headers every test request carries unless the user overrides them in the headers editor. */
const DEFAULT_TEST_HEADERS: Record<string, string> = {
    "Content-Type": "application/json",
};

/**
 * Webhook-specific extras for the trigger settings sidebar: the endpoint URL (so it can be
 * copied while setting up the signing secret) and a test request that stands in for an
 * external caller while the automation is being developed.
 */
@customElement("ua-webhook-trigger-panel")
export class UaWebhookTriggerPanelElement extends UmbLitElement {
    /** Unique of the automation this webhook belongs to. */
    @property({ type: String, attribute: "automation-id" })
    automationId = "";

    /** HTTP method the webhook accepts, taken from the trigger's settings. */
    @property({ type: String, attribute: "allowed-method" })
    allowedMethod = "POST";

    @state()
    private _body = "";

    @state()
    private _headers = "";

    @state()
    private _sending = false;

    get #isSaved(): boolean {
        return !!this.automationId && this.automationId !== UA_EMPTY_GUID;
    }

    override render() {
        return html`
            <uui-box headline=${this.localize.term("uaWebhook_headline")}>
                ${this.#isSaved ? this.#renderUrl() : this.#renderUnsavedHint()}
                ${this.#isSaved ? this.#renderTestRequest() : nothing}
            </uui-box>
        `;
    }

    #renderUnsavedHint() {
        return html`<p class="hint">${this.localize.term("uaWebhook_unsavedHint")}</p>`;
    }

    #renderUrl() {
        return html`
            <umb-property-layout label=${this.localize.term("uaLabels_webhookUrl")} orientation="vertical">
                <ua-webhook-url-field slot="editor" automation-id=${this.automationId}></ua-webhook-url-field>
            </umb-property-layout>
        `;
    }

    #renderTestRequest() {
        return html`
            <umb-property-layout
                label=${this.localize.term("uaWebhook_testHeadline")}
                description=${this.localize.term("uaWebhook_testDescription")}
                orientation="vertical"
            >
                <umb-code-editor
                    slot="editor"
                    language="json"
                    disable-minimap
                    word-wrap
                    .code=${this._body}
                    @input=${(e: Event) => (this._body = (e.target as UmbCodeEditorElement).code)}
                ></umb-code-editor>
            </umb-property-layout>

            <umb-property-layout
                label=${this.localize.term("uaWebhook_testHeadersLabel")}
                description=${this.localize.term("uaWebhook_testHeadersDescription")}
                orientation="vertical"
            >
                <umb-code-editor
                    slot="editor"
                    class="short"
                    language="json"
                    disable-minimap
                    word-wrap
                    .code=${this._headers}
                    @input=${(e: Event) => (this._headers = (e.target as UmbCodeEditorElement).code)}
                ></umb-code-editor>
            </umb-property-layout>

            <uui-button
                look="secondary"
                ?disabled=${this._sending}
                label=${this.localize.term("uaWebhook_sendTest")}
                @click=${this.#onSendTest}
            >
                ${this.localize.term("uaWebhook_sendTest")}
            </uui-button>
        `;
    }

    async #onSendTest() {
        const headers = this.#parseHeaders();
        if (headers === undefined) return;

        this._sending = true;
        try {
            // The real endpoint hands steps the body verbatim as a string, so send the pasted
            // text as-is rather than a parsed object — malformed JSON is the caller's business
            // and a step under test may well want to see it.
            const { error } = await AutomationsService.postAutomationsByIdTrigger({
                path: { id: this.automationId },
                body: {
                    triggerOutputData: {
                        method: this.allowedMethod || "POST",
                        body: this._body,
                        headers,
                        query: {},
                    },
                },
            });

            if (error) {
                // Typically 409 when the automation isn't published — the server's problem
                // detail says why, so prefer it over generic copy.
                this.#notify(
                    "danger",
                    (error as { detail?: string } | undefined)?.detail ??
                        this.localize.term("uaWebhook_testFailed"),
                );
                return;
            }

            dispatchActionEvent(this, new UaAutomationRunsChangedEvent(this.automationId));
            this.#notify("positive", this.localize.term("uaWebhook_testSent"));
        } finally {
            this._sending = false;
        }
    }

    /**
     * Parses the headers editor, layered over the defaults. Returns `undefined` (and
     * notifies) when the text isn't a JSON object, so the caller can abort the send.
     */
    #parseHeaders(): Record<string, string> | undefined {
        if (!this._headers.trim()) return { ...DEFAULT_TEST_HEADERS };

        try {
            const parsed = JSON.parse(this._headers);
            if (typeof parsed !== "object" || parsed === null || Array.isArray(parsed)) {
                throw new Error("Not an object");
            }
            return { ...DEFAULT_TEST_HEADERS, ...parsed };
        } catch {
            this.#notify("danger", this.localize.term("uaWebhook_testHeadersInvalid"));
            return undefined;
        }
    }

    async #notify(color: "positive" | "danger", message: string) {
        const notifications = await this.getContext(UMB_NOTIFICATION_CONTEXT);
        notifications?.peek(color, { data: { message } });
    }

    static styles = [
        UmbTextStyles,
        css`
            /* Match ua-settings-form so the panel's rows sit on the same rhythm as the
               settings fields directly above it. */
            umb-property-layout {
                --uui-size-layout-1: var(--uui-size-space-2);
            }

            .hint {
                margin: 0;
                color: var(--uui-color-text-alt);
            }

            /* Same framing the CMS code editor property editor UI applies. */
            umb-code-editor {
                display: block;
                height: 160px;
                border-radius: var(--uui-border-radius);
                border: 1px solid var(--uui-color-divider-emphasis);
            }

            umb-code-editor.short {
                height: 100px;
            }
        `,
    ];
}

export default UaWebhookTriggerPanelElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-webhook-trigger-panel": UaWebhookTriggerPanelElement;
    }
}
