export interface UaApprovalDecisionModalData {
    runId: string;
    stepId: string;
    automationName: string;
    prompt: string | null;
}

export interface UaApprovalDecisionModalValue {
    outcome: "Approved" | "Rejected";
}
