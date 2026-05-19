import { customElement, html, property } from "@umbraco-cms/backoffice/external/lit";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import type { UmbPropertyEditorUiElement } from "@umbraco-cms/backoffice/property-editor";

// Thin wrapper around CMS's <umb-user-group-input> exposed as a property editor UI.
// CMS ships the input element and a picker modal, but no registered property-editor-ui
// for user groups (unlike MemberGroupPicker), so we register our own.
@customElement("ua-user-group-picker")
export class UaUserGroupPickerElement extends UmbLitElement implements UmbPropertyEditorUiElement {
    @property()
    public value?: string;

    #onChange(event: CustomEvent) {
        const target = event.target as HTMLElement & { value?: string };
        this.value = target.value;
        this.dispatchEvent(new UmbChangeEvent());
    }

    override render() {
        return html`
            <umb-user-group-input .value=${this.value ?? ""} @change=${this.#onChange}>
            </umb-user-group-input>
        `;
    }
}

export default UaUserGroupPickerElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-user-group-picker": UaUserGroupPickerElement;
    }
}
