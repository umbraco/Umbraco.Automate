import { UmbModalToken } from "@umbraco-cms/backoffice/modal";
import type { UaConditionModalData, UaConditionModalValue } from "./types.js";

export const UA_CONDITION_MODAL = new UmbModalToken<UaConditionModalData, UaConditionModalValue>(
    "UmbracoAutomate.Modal.Condition",
    {
        modal: {
            type: "sidebar",
            size: "small",
        },
    },
);
