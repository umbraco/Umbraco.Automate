import { UmbModalToken } from "@umbraco-cms/backoffice/modal";
import type { ConditionSetModel } from "../../workspace/automation/canvas/types.js";

export interface UaEdgeFilterModalData {
    filter: ConditionSetModel | null;
}

export interface UaEdgeFilterModalValue {
    filter: ConditionSetModel | null;
}

export const UA_EDGE_FILTER_MODAL = new UmbModalToken<UaEdgeFilterModalData, UaEdgeFilterModalValue>(
    "UmbracoAutomate.Modal.EdgeFilter",
    {
        modal: {
            type: "sidebar",
            size: "medium",
        },
    },
);
