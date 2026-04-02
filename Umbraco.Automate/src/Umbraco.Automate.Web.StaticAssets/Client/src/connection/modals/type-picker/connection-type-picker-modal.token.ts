import { UmbModalToken } from "@umbraco-cms/backoffice/modal";

export interface UaConnectionTypePickerModalValue {
    typeAlias: string;
}

export const UA_CONNECTION_TYPE_PICKER_MODAL = new UmbModalToken<never, UaConnectionTypePickerModalValue>(
    "Ua.Modal.ConnectionTypePicker",
    {
        modal: {
            type: "sidebar",
            size: "small",
        },
    },
);
