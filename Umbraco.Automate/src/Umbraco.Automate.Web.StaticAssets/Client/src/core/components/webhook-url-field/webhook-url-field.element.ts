import { css, html, customElement, property } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";
import { buildWebhookUrl } from "../../utils/webhook-url.utils.js";

/**
 * Read-only display of an automation's webhook endpoint URL, with a copy button.
 * Shared by the trigger settings sidebar and the automation Info view so the two
 * can't drift apart.
 */
@customElement("ua-webhook-url-field")
export class UaWebhookUrlFieldElement extends UmbLitElement {
    /** Unique of the automation whose webhook URL should be shown. */
    @property({ type: String, attribute: "automation-id" })
    automationId = "";

    override render() {
        const url = buildWebhookUrl(this.automationId);

        return html`
            <uui-input readonly .value=${url}></uui-input>
            <uui-button
                compact
                look="secondary"
                label=${this.localize.term("uaWebhook_copyUrl")}
                @click=${() => this.#copy(url)}
            >
                <uui-icon name="icon-clipboard-copy"></uui-icon>
            </uui-button>
        `;
    }

    async #copy(url: string) {
        const notifications = await this.getContext(UMB_NOTIFICATION_CONTEXT);
        try {
            await navigator.clipboard.writeText(url);
            notifications?.peek("positive", {
                data: { message: this.localize.term("uaWebhook_urlCopied") },
            });
        } catch {
            notifications?.peek("danger", {
                data: { message: this.localize.term("uaWebhook_urlCopyFailed") },
            });
        }
    }

    static styles = [
        css`
            :host {
                display: flex;
                align-items: center;
                gap: var(--uui-size-space-2);
            }

            uui-input {
                flex: 1;
                font-family: var(--uui-font-family-mono, monospace);
            }
        `,
    ];
}

export default UaWebhookUrlFieldElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-webhook-url-field": UaWebhookUrlFieldElement;
    }
}
