import { customElement, html, property } from "@umbraco-cms/backoffice/external/lit";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import type { UmbPropertyEditorUiElement } from "@umbraco-cms/backoffice/property-editor";

// Thin wrapper around CMS's <umb-input-member-type> exposed as a property editor UI.
// CMS doesn't ship a MemberTypePicker property-editor-ui out of the box (unlike
// MediaTypePicker), so we register our own and round-trip the comma-separated GUID
// value the underlying input emits.
@customElement("ua-member-type-picker")
export class UaMemberTypePickerElement extends UmbLitElement implements UmbPropertyEditorUiElement {
    @property()
    public value?: string;

    #onChange(event: CustomEvent) {
        const target = event.target as HTMLElement & { value?: string };
        this.value = target.value;
        this.dispatchEvent(new UmbChangeEvent());
    }

    override render() {
        return html`
            <umb-input-member-type .value=${this.value ?? ""} @change=${this.#onChange}>
            </umb-input-member-type>
        `;
    }
}

export default UaMemberTypePickerElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-member-type-picker": UaMemberTypePickerElement;
    }
}
