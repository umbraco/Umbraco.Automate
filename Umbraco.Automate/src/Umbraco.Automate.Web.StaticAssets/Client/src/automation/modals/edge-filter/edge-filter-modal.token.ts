import { UmbModalToken } from "@umbraco-cms/backoffice/modal";
import type { ConditionSetModel } from "../../workspace/automation/canvas/types.js";
import type { StepConfigurationModel, StepConnectionModel, TriggerConfigurationModel } from "../../../api/types.gen.js";

export interface UaEdgeFilterModalData {
    filter: ConditionSetModel | null;
    /** Target step of the edge — defines what binding sources are reachable. */
    targetStepId?: string;
    automationContext?: {
        trigger: TriggerConfigurationModel | null;
        steps: StepConfigurationModel[];
        connections: StepConnectionModel[];
    };
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
