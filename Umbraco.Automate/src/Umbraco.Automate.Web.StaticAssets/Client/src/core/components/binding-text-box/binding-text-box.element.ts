import { css, customElement, html, property, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UMB_VALIDATION_EMPTY_LOCALIZATION_KEY, UmbFormControlMixin } from "@umbraco-cms/backoffice/validation";
import type { UUIInputElement } from "@umbraco-cms/backoffice/external/uui";
import type {
    UmbPropertyEditorUiElement,
    UmbPropertyEditorConfigCollection,
} from "@umbraco-cms/backoffice/property-editor";

/**
 * A text box property editor for binding-enabled fields.
 * Identical to the built-in TextBox but registered under our own alias so
 * the "Insert binding" property action targets only these fields.
 *
 * The binding picker is provided by a `propertyAction` manifest, not inline.
 */
@customElement("ua-binding-text-box")
export class UaBindingTextBoxElement
    extends UmbFormControlMixin<string, typeof UmbLitElement, undefined>(UmbLitElement, undefined)
    implements UmbPropertyEditorUiElement
{
    @property({ type: Boolean, reflect: true })
    readonly = false;

    @property({ type: Boolean })
    mandatory?: boolean;

    @property({ type: String })
    mandatoryMessage = UMB_VALIDATION_EMPTY_LOCALIZATION_KEY;

    @property({ type: String })
    name?: string;

    @state()
    private _placeholder?: string;

    public set config(config: UmbPropertyEditorConfigCollection | undefined) {
        if (!config) return;
        this._placeholder = config.getValueByAlias<string>("placeholder") ?? "";
    }

    protected override firstUpdated(): void {
        this.addFormControlElement(this.shadowRoot!.querySelector("uui-input")!);
    }

    override focus() {
        return this.shadowRoot?.querySelector<UUIInputElement>("uui-input")?.focus();
    }

    #onInput(e: InputEvent) {
        const newValue = (e.target as HTMLInputElement).value;
        if (newValue === this.value) return;
        this.value = newValue;
        this.dispatchEvent(new UmbChangeEvent());
    }

    override render() {
        return html`
            <uui-input
                .label=${this.name ?? ""}
                .placeholder=${this._placeholder ?? ""}
                .requiredMessage=${this.mandatoryMessage}
                .value=${this.value ?? ""}
                ?readonly=${this.readonly}
                ?required=${this.mandatory}
                @input=${this.#onInput}
            >
            </uui-input>
        `;
    }

    static override styles = [
        css`
            uui-input {
                width: 100%;
            }
        `,
    ];
}

export default UaBindingTextBoxElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-binding-text-box": UaBindingTextBoxElement;
    }
}
