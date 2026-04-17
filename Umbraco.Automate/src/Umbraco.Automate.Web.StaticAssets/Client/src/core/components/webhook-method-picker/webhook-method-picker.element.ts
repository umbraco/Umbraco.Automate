import { css, customElement, html, property } from "@umbraco-cms/backoffice/external/lit";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbFormControlMixin } from "@umbraco-cms/backoffice/validation";
import type { UmbPropertyEditorUiElement } from "@umbraco-cms/backoffice/property-editor";
import type { UUISelectElement, UUISelectEvent } from "@umbraco-cms/backoffice/external/uui";

const METHODS = ["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD"] as const;
const DEFAULT_METHOD = "POST";

@customElement("ua-webhook-method-picker")
export class UaWebhookMethodPickerElement
    extends UmbFormControlMixin<string, typeof UmbLitElement, undefined>(UmbLitElement, undefined)
    implements UmbPropertyEditorUiElement
{
    @property({ type: Boolean, reflect: true })
    readonly = false;

    #selfUpdated = false;

    protected override updated() {
        if (this.#selfUpdated) {
            this.#selfUpdated = false;
            return;
        }
        if (!this.value && !this.readonly) {
            this.#selfUpdated = true;
            this.value = DEFAULT_METHOD;
            this.dispatchEvent(new UmbChangeEvent());
        }
    }

    #onChange(event: UUISelectEvent) {
        const next = (event.target as UUISelectElement).value as string;
        if (next === this.value) return;
        this.#selfUpdated = true;
        this.value = next;
        this.dispatchEvent(new UmbChangeEvent());
    }

    override render() {
        const options = METHODS.map((m) => ({
            name: m,
            value: m,
            selected: m === this.value,
        }));

        return html`
            <uui-select
                label="Allowed Method"
                .options=${options}
                ?disabled=${this.readonly}
                @change=${this.#onChange}
            ></uui-select>
        `;
    }

    static override styles = [
        css`
            :host {
                display: block;
            }

            uui-select {
                width: 100%;
            }
        `,
    ];
}

export default UaWebhookMethodPickerElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-webhook-method-picker": UaWebhookMethodPickerElement;
    }
}
