import { customElement, html, property, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import type { UmbInputElementElement } from "@umbraco-cms/backoffice/element";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import type {
    UmbPropertyEditorConfigCollection,
    UmbPropertyEditorUiElement,
} from "@umbraco-cms/backoffice/property-editor";
import { UMB_VALIDATION_EMPTY_LOCALIZATION_KEY, UmbFormControlMixin } from "@umbraco-cms/backoffice/validation";

/**
 * Property editor UI that picks an element or element folder from the Library and stores the key
 * as a string. Used by settings fields that hold an element key. Binding support is not built in:
 * the generic `ua-bindable-editor` wrapper adds the switch to a `${ }` expression around it.
 *
 * Config: `folderOnly` (boolean, default false). When true the picker lists folders only;
 * otherwise it lists elements only.
 */
@customElement("ua-element-key-picker")
export class UaElementKeyPickerElement
    extends UmbFormControlMixin<string | undefined, typeof UmbLitElement, undefined>(UmbLitElement, undefined)
    implements UmbPropertyEditorUiElement
{
    @property({ type: Boolean })
    mandatory?: boolean;

    @property({ type: String })
    mandatoryMessage = UMB_VALIDATION_EMPTY_LOCALIZATION_KEY;

    @property({ type: Boolean, reflect: true })
    readonly = false;

    public set config(config: UmbPropertyEditorConfigCollection | undefined) {
        if (!config) return;

        this._folderOnly = Boolean(config.getValueByAlias("folderOnly"));
    }

    @state()
    private _folderOnly = false;

    constructor() {
        super();
        // Enforce mandatory on the host: the editor stores a single key, so empty means no value.
        this.addValidator(
            "valueMissing",
            () => this.mandatoryMessage,
            () => !this.readonly && !!this.mandatory && !this.value,
        );
    }

    protected override firstUpdated() {
        this.addFormControlElement(this.shadowRoot!.querySelector("umb-input-element")!);
    }

    #onChange(event: CustomEvent & { target: UmbInputElementElement }) {
        // umb-input-element holds a comma-separated list; with max 1 it is empty or a single key.
        this.value = event.target.value || undefined;
        this.dispatchEvent(new UmbChangeEvent());
    }

    override render() {
        return html`
            <umb-input-element
                .min=${0}
                .max=${1}
                .value=${this.value}
                .folderOnly=${this._folderOnly}
                ?readonly=${this.readonly}
                @change=${this.#onChange}
            ></umb-input-element>
        `;
    }
}

export default UaElementKeyPickerElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-element-key-picker": UaElementKeyPickerElement;
    }
}
