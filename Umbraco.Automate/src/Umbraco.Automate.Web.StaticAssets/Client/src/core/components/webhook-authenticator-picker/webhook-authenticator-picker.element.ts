import { css, customElement, html, nothing, property, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbFormControlMixin } from "@umbraco-cms/backoffice/validation";
import type { UmbPropertyEditorUiElement } from "@umbraco-cms/backoffice/property-editor";
import type { UUISelectElement, UUISelectEvent } from "@umbraco-cms/backoffice/external/uui";
import { client } from "../../../api/client.gen.js";
import type { EditableModelFieldDescriptorModel, EditableModelSchemaModel } from "../../../api/types.gen.js";
import type { SettingsChangeDetail } from "../settings-form/settings-form.element.js";
import "../settings-form/settings-form.element.js";

interface WebhookAuthenticatorItem {
    alias: string;
    name: string;
    description?: string;
    settingsSchema?: EditableModelSchemaModel | null;
}

interface WebhookAuthenticatorValue {
    alias: string;
    settings: Record<string, unknown>;
}

const ENDPOINT = "/umbraco/automate/management/api/v1/catalogue/webhook-authenticators";
const PLAIN_SECRET_ALIAS = "plain-secret";

@customElement("ua-webhook-authenticator-picker")
export class UaWebhookAuthenticatorPickerElement
    extends UmbFormControlMixin<WebhookAuthenticatorValue | undefined, typeof UmbLitElement, undefined>(
        UmbLitElement,
        undefined,
    )
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

            if (!this.value?.alias) {
                const fallback = data.find((d) => d.alias === PLAIN_SECRET_ALIAS) ?? data[0];
                if (fallback) {
                    this.#setValue({ alias: fallback.alias, settings: {} });
                }
            }
        } catch {
            this._error = "Could not load authentication strategies.";
        } finally {
            this._loading = false;
        }
    }

    #setValue(next: WebhookAuthenticatorValue) {
        this.value = next;
        this.dispatchEvent(new UmbChangeEvent());
    }

    #onStrategyChange(event: UUISelectEvent) {
        const alias = (event.target as UUISelectElement).value as string;
        if (alias === this.value?.alias) return;
        // Switching strategy drops the previous strategy's settings — different strategies
        // use different fields, so carrying them across would be noise. If the user picks
        // the same strategy again later, they refill.
        this.#setValue({ alias, settings: {} });
    }

    #onSettingsChange(event: CustomEvent<SettingsChangeDetail>) {
        if (!this.value) return;
        this.#setValue({ alias: this.value.alias, settings: event.detail.settings });
    }

    get #selected(): WebhookAuthenticatorItem | undefined {
        return this._options.find((o) => o.alias === this.value?.alias);
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
            selected: opt.alias === this.value?.alias,
        }));

        const selected = this.#selected;
        const fields = (selected?.settingsSchema?.fields ?? []) as EditableModelFieldDescriptorModel[];

        return html`
            <div class="picker">
                <uui-select
                    label="Authentication Strategy"
                    .options=${items}
                    ?disabled=${this.readonly}
                    @change=${this.#onStrategyChange}
                ></uui-select>
                ${selected?.description ? html`<small class="hint">${selected.description}</small>` : nothing}
            </div>
            ${fields.length > 0
                ? html`
                      <div class="strategy-settings">
                          <ua-settings-form
                              no-box
                              .fields=${fields}
                              .values=${(this.value?.settings ?? {}) as Record<string, unknown>}
                              @ua:settings-change=${this.#onSettingsChange}
                          ></ua-settings-form>
                      </div>
                  `
                : nothing}
        `;
    }

    static override styles = [
        css`
            :host {
                display: flex;
                flex-direction: column;
                gap: var(--uui-size-layout-1);
            }

            .picker {
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

            .strategy-settings {
                padding-top: var(--uui-size-space-3);
                border-top: 1px solid var(--uui-color-divider);
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
