import { customElement, html } from "@umbraco-cms/backoffice/external/lit";
import { UaEntityKeyPickerElementBase } from "./entity-key-picker.base.js";

/**
 * Content key editor: pick a document off the content tree, or switch to a `${ }` binding.
 * Wraps CMS's `<umb-input-document>`, capped at one node because the settings field holds a
 * single key. See the base class for how the two modes are resolved.
 */
@customElement("ua-content-key-picker")
export class UaContentKeyPickerElement extends UaEntityKeyPickerElementBase {
    protected override renderPicker() {
        return html`
            <umb-input-document
                .min=${0}
                .max=${1}
                .value=${this.pickerValue}
                ?readonly=${this.readonly}
                @change=${this.onPickerChange}
            >
            </umb-input-document>
        `;
    }
}

export default UaContentKeyPickerElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-content-key-picker": UaContentKeyPickerElement;
    }
}
