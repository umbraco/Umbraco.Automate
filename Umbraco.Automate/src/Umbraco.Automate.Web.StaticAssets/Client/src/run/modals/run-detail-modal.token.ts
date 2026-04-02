import { UmbModalToken } from "@umbraco-cms/backoffice/modal";

export interface UaRunDetailModalData {
    runId: string;
}

export const UA_RUN_DETAIL_MODAL = new UmbModalToken<UaRunDetailModalData, never>(
    "Ua.Modal.RunDetail",
    {
        modal: {
            type: "sidebar",
            size: "large",
        },
    },
);
