import { UmbModalToken } from "@umbraco-cms/backoffice/modal";
import type { UaApprovalDecisionModalData, UaApprovalDecisionModalValue } from "./types.js";

export const UA_APPROVAL_DECISION_MODAL = new UmbModalToken<UaApprovalDecisionModalData, UaApprovalDecisionModalValue>(
    "UmbracoAutomate.Modal.ApprovalDecision",
    {
        modal: {
            type: "sidebar",
            size: "small",
        },
    },
);
