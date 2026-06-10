import { UmbModalToken } from "@umbraco-cms/backoffice/modal";
import type { UaNodeSettingsModalData, UaNodeSettingsModalValue } from "./types.js";

export const UA_NODE_SETTINGS_MODAL = new UmbModalToken<UaNodeSettingsModalData, UaNodeSettingsModalValue>(
    "UmbracoAutomate.Modal.NodeSettings",
    {
        modal: {
            type: "sidebar",
            size: "medium",
        },
    },
);
