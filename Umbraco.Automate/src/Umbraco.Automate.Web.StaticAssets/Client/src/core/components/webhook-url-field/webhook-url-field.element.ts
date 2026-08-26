import { css, html, customElement, property, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";
import { tryExecute } from "@umbraco-cms/backoffice/resources";
import { AutomationsService } from "../../../api/sdk.gen.js";

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

    @state()
    private _url: string | null = null;

    @state()
    private _loading = false;

    override updated(changed: Map<PropertyKey, unknown>) {
        super.updated(changed);
        if (changed.has("automationId") && this.automationId) {
            this.#loadUrl();
        }
    }

    override render() {
        return html`
            <uui-input readonly .value=${this._url ?? ""} ?disabled=${this._loading}></uui-input>
            <uui-button
                compact
                look="secondary"
                ?disabled=${!this._url}
                label=${this.localize.term("uaWebhook_copyUrl")}
                @click=${() => this.#copy(this._url!)}
            >
                <uui-icon name="icon-clipboard-copy"></uui-icon>
            </uui-button>
        `;
    }

    async #loadUrl() {
        this._loading = true;
        this._url = null;

        // Resolved server-side from Umbraco's configured application URL rather than this
        // browser's own address bar, so it stays correct behind a load balancer.
        const { data, error } = await tryExecute(
            this,
            AutomationsService.getAutomationsByIdWebhookUrl({ path: { id: this.automationId } }),
        );

        if (error) {
            const notifications = await this.getContext(UMB_NOTIFICATION_CONTEXT);
            notifications?.peek("danger", { data: { message: this.localize.term("uaWebhook_urlLoadFailed") } });
        }

        this._url = data?.url ?? null;
        this._loading = false;
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
