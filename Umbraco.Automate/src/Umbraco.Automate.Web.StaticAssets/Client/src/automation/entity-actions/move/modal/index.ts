import { UmbModalToken } from "@umbraco-cms/backoffice/modal";

export interface UaAutomationMoveModalData {
    unique: string;
}

export const UA_AUTOMATION_MOVE_MODAL = new UmbModalToken<UaAutomationMoveModalData>(
    "UmbracoAutomate.Modal.AutomationMove",
    {
        modal: {
            type: "sidebar",
            size: "small",
        },
    },
);
