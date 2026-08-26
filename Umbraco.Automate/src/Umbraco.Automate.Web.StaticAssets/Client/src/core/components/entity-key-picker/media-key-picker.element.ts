import { customElement, html } from "@umbraco-cms/backoffice/external/lit";
import { UaEntityKeyPickerElementBase } from "./entity-key-picker.base.js";

/**
 * Media key editor: pick an item off the media tree, or switch to a `${ }` binding.
 * The media counterpart of `ua-content-key-picker`; see the base class for the mode rules.
 */
@customElement("ua-media-key-picker")
export class UaMediaKeyPickerElement extends UaEntityKeyPickerElementBase {
    protected override renderPicker() {
        return html`
            <umb-input-media
                .min=${0}
                .max=${1}
                .value=${this.pickerValue}
                ?readonly=${this.readonly}
                @change=${this.onPickerChange}
            >
            </umb-input-media>
        `;
    }
}

export default UaMediaKeyPickerElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-media-key-picker": UaMediaKeyPickerElement;
    }
}
