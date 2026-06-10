import { html, customElement } from "@umbraco-cms/backoffice/external/lit";
import { UmbModalBaseElement } from "@umbraco-cms/backoffice/modal";
import type { UaBindingPickerModalData, UaBindingPickerModalValue } from "./binding-picker-modal.token.js";
import "../binding-picker/binding-picker.element.js";

@customElement("ua-binding-picker-modal")
export class UaBindingPickerModalElement extends UmbModalBaseElement<
    UaBindingPickerModalData,
    UaBindingPickerModalValue
> {
    #onBindingSelect(e: CustomEvent<{ expression: string }>) {
        this.value = { expression: e.detail.expression };
        this.modalContext?.submit();
    }

    #onClose() {
        this.modalContext?.reject();
    }

    override render() {
        if (!this.data) return html``;

        return html`
            <umb-body-layout headline=${this.localize.term("uaBindings_insertExpression")}>
                <ua-binding-picker
                    .sources=${this.data.sources}
                    @ua:binding-select=${this.#onBindingSelect}
                ></ua-binding-picker>
                <div slot="actions">
                    <uui-button
                        label=${this.localize.term("uaGeneral_close")}
                        @click=${this.#onClose}
                    ></uui-button>
                </div>
            </umb-body-layout>
        `;
    }
}

export default UaBindingPickerModalElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-binding-picker-modal": UaBindingPickerModalElement;
    }
}
