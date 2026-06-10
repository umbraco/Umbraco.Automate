import { UmbModalToken } from "@umbraco-cms/backoffice/modal";
import type { UaNodePickerModalData, UaNodePickerModalValue } from "./types.js";

export const UA_NODE_PICKER_MODAL = new UmbModalToken<UaNodePickerModalData, UaNodePickerModalValue>(
    "UmbracoAutomate.Modal.NodePicker",
    {
        modal: {
            type: "sidebar",
            size: "small",
        },
    },
);
