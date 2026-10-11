import { customElement, html, property, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbMediaPickerFolderFilter, type UmbInputMediaElement } from "@umbraco-cms/backoffice/media";
import type { UmbNumberRangeValueType } from "@umbraco-cms/backoffice/models";
import type {
    UmbPropertyEditorConfigCollection,
    UmbPropertyEditorUiElement,
} from "@umbraco-cms/backoffice/property-editor";
import { UMB_VALIDATION_EMPTY_LOCALIZATION_KEY, UmbFormControlMixin } from "@umbraco-cms/backoffice/validation";

const FOLDER_FILTERS = Object.values(UmbMediaPickerFolderFilter);

function isFolderFilter(value: string | undefined): value is UmbMediaPickerFolderFilter {
    return FOLDER_FILTERS.some((filter) => filter === value);
}

/**
 * Property editor UI that picks media off the media tree and stores the key as a string.
 * Used by settings fields that hold a media key. Binding support is not built in: the generic
 * `ua-bindable-editor` wrapper adds the switch to a `${ }` expression around it.
 *
 * Config: `validationLimit` ({ min, max }, default max 1) and `folderFilter`
 * (`filesOnly` (default) | `filesAndFolders` | `foldersOnly`).
 */
@customElement("ua-media-key-picker")
export class UaMediaKeyPickerElement
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
        this._max = minMax?.max ?? 1;

        const folderFilter = config.getValueByAlias<string>("folderFilter");
        this._folderFilter = isFolderFilter(folderFilter) ? folderFilter : UmbMediaPickerFolderFilter.FILES_ONLY;
    }

    @state()
    private _min = 0;

    @state()
    private _max = 1;

    @state()
    private _folderFilter = UmbMediaPickerFolderFilter.FILES_ONLY;

    constructor() {
        super();
        // umb-input-media has no required/valueMissing support of its own, so enforce mandatory on the host.
        this.addValidator(
            "valueMissing",
            () => this.mandatoryMessage,
            () => !this.readonly && !!this.mandatory && !this.value,
        );
    }

    protected override firstUpdated() {
        this.addFormControlElement(this.shadowRoot!.querySelector("umb-input-media")!);
    }

    #onChange(event: CustomEvent & { target: UmbInputMediaElement }) {
        this.value = event.target.value;
        this.dispatchEvent(new UmbChangeEvent());
    }

    override render() {
        return html`
            <umb-input-media
                .min=${this._min}
                .max=${this._max}
                .value=${this.value}
                .folderFilter=${this._folderFilter}
                ?readonly=${this.readonly}
                @change=${this.#onChange}
            ></umb-input-media>
        `;
    }
}

export default UaMediaKeyPickerElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-media-key-picker": UaMediaKeyPickerElement;
    }
}
