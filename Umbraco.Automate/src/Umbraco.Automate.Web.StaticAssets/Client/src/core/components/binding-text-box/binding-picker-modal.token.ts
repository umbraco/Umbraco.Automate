import { UmbModalToken } from "@umbraco-cms/backoffice/modal";
import type { BindingSource } from "../../utils/binding-context.utils.js";

export interface UaBindingPickerModalData {
    sources: BindingSource[];
}

export interface UaBindingPickerModalValue {
    expression: string;
}

export const UA_BINDING_PICKER_MODAL = new UmbModalToken<UaBindingPickerModalData, UaBindingPickerModalValue>(
    "Ua.Modal.BindingPicker",
    {
        modal: {
            type: "sidebar",
            size: "small",
        },
    },
);
