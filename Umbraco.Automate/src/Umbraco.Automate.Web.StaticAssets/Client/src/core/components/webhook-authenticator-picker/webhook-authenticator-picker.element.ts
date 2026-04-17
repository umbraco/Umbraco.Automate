import { css, customElement, html, nothing, property, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbFormControlMixin } from "@umbraco-cms/backoffice/validation";
import type { UmbPropertyEditorUiElement } from "@umbraco-cms/backoffice/property-editor";
import type { UUISelectElement, UUISelectEvent } from "@umbraco-cms/backoffice/external/uui";
import { client } from "../../../api/client.gen.js";

interface WebhookAuthenticatorItem {
    alias: string;
    name: string;
    description?: string;
}

const ENDPOINT = "/umbraco/automate/management/api/v1/catalogue/webhook-authenticators";
const PLAIN_SECRET_ALIAS = "plain-secret";

@customElement("ua-webhook-authenticator-picker")
export class UaWebhookAuthenticatorPickerElement
    extends UmbFormControlMixin<string, typeof UmbLitElement, undefined>(UmbLitElement, undefined)
    implements UmbPropertyEditorUiElement
{
    @property({ type: Boolean, reflect: true })
    readonly = false;

    @state()
    private _options: WebhookAuthenticatorItem[] = [];

    @state()
    private _loading = true;

    @state()
    private _error?: string;

    override connectedCallback(): void {
        super.connectedCallback();
        void this.#loadAuthenticators();
    }

    async #loadAuthenticators() {
        this._loading = true;
        this._error = undefined;

        try {
            const { data, error } = await client.get<WebhookAuthenticatorItem[]>({
                url: ENDPOINT,
                security: [{ scheme: "bearer", type: "http" }],
            });

            if (error || !data) {
                this._error = "Could not load authentication strategies.";
                return;
            }

            this._options = data;

            // Seed the default when no value is set yet so the form persists something sensible.
            if (!this.value) {
                const fallback = data.find((d) => d.alias === PLAIN_SECRET_ALIAS) ?? data[0];
                if (fallback) {
                    this.value = fallback.alias;
                    this.dispatchEvent(new UmbChangeEvent());
                }
            }
        } catch {
            this._error = "Could not load authentication strategies.";
        } finally {
            this._loading = false;
        }
    }

    #onChange(event: UUISelectEvent) {
        const next = (event.target as UUISelectElement).value as string;
        if (next === this.value) return;
        this.value = next;
        this.dispatchEvent(new UmbChangeEvent());
    }

    get #selected(): WebhookAuthenticatorItem | undefined {
        return this._options.find((o) => o.alias === this.value);
    }

    override render() {
        if (this._loading) {
            return html`<uui-loader-bar></uui-loader-bar>`;
        }

        if (this._error) {
            return html`<div class="error">${this._error}</div>`;
        }

        const items = this._options.map((opt) => ({
            name: opt.name,
            value: opt.alias,
            selected: opt.alias === this.value,
        }));

        const hint = this.#selected?.description;

        return html`
            <uui-select
                label="Authentication Strategy"
                .options=${items}
                ?disabled=${this.readonly}
                @change=${this.#onChange}
            ></uui-select>
            ${hint ? html`<small class="hint">${hint}</small>` : nothing}
        `;
    }

    static override styles = [
        css`
            :host {
                display: flex;
                flex-direction: column;
                gap: var(--uui-size-space-2);
            }

            uui-select {
                width: 100%;
            }

            .hint {
                color: var(--uui-color-text-alt);
                line-height: 1.4;
            }

            .error {
                color: var(--uui-color-danger);
            }
        `,
    ];
}

export default UaWebhookAuthenticatorPickerElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-webhook-authenticator-picker": UaWebhookAuthenticatorPickerElement;
    }
}
