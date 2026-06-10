import { UmbModalToken } from "@umbraco-cms/backoffice/modal";
import type { UaTriggerSettingsModalData, UaTriggerSettingsModalValue } from "./types.js";

export const UA_TRIGGER_SETTINGS_MODAL = new UmbModalToken<UaTriggerSettingsModalData, UaTriggerSettingsModalValue>(
    "UmbracoAutomate.Modal.TriggerSettings",
    {
        modal: {
            type: "sidebar",
            size: "small",
        },
    },
);
