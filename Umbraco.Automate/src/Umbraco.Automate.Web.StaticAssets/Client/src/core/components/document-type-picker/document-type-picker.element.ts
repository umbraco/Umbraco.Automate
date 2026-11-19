import { customElement, html, property, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import type { UmbInputDocumentTypeElement } from "@umbraco-cms/backoffice/document-type";
import type { UmbNumberRangeValueType } from "@umbraco-cms/backoffice/models";
import type {
    UmbPropertyEditorConfigCollection,
    UmbPropertyEditorUiElement,
} from "@umbraco-cms/backoffice/property-editor";
import { UMB_VALIDATION_EMPTY_LOCALIZATION_KEY, UmbFormControlMixin } from "@umbraco-cms/backoffice/validation";

/**
 * Property editor UI that picks document types, like the CMS's own `DocumentTypePicker`, but
 * enforces `mandatory`. The CMS editor ignores it (its minimum only fails once something is
 * picked), so a required field would save empty.
 *
 * Config: `validationLimit` ({ min, max }) and `onlyPickElementTypes` (boolean), the CMS's keys.
 */
@customElement("ua-document-type-picker")
export class UaDocumentTypePickerElement
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

        const minMax = config.getValueByAlias<UmbNumberRangeValueType>("validationLimit");
        this._min = minMax?.min ?? 0;
        this._max = minMax?.max ?? Infinity;
        this._elementTypesOnly = Boolean(config.getValueByAlias("onlyPickElementTypes"));
    }

    @state()
    private _min = 0;

    @state()
    private _max = Infinity;

    @state()
    private _elementTypesOnly = false;

    constructor() {
        super();
        this.addValidator(
            "valueMissing",
            () => this.mandatoryMessage,
            () => !this.readonly && !!this.mandatory && !this.value,
        );
    }

    protected override firstUpdated() {
        this.addFormControlElement(this.shadowRoot!.querySelector("umb-input-document-type")!);
    }

    #onChange(event: CustomEvent & { target: UmbInputDocumentTypeElement }) {
        this.value = event.target.value || undefined;
        this.dispatchEvent(new UmbChangeEvent());
    }

    override render() {
        return html`
            <umb-input-document-type
                .min=${this._min}
                .max=${this._max}
                .value=${this.value}
                .elementTypesOnly=${this._elementTypesOnly}
                ?readonly=${this.readonly}
                @change=${this.#onChange}
            ></umb-input-document-type>
        `;
    }
}

export default UaDocumentTypePickerElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-document-type-picker": UaDocumentTypePickerElement;
    }
}
