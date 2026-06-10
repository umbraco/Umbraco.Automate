import { UA_AUTOMATION_ENTITY_TYPE } from "../../constants.js";

export const automationMoveManifests: Array<UmbExtensionManifest> = [
    {
        type: "entityAction",
        kind: "default",
        alias: "UmbracoAutomate.EntityAction.Automation.Move",
        name: "Move Automation Entity Action",
        weight: 200,
        api: () => import("./move-automation.action.js"),
        forEntityTypes: [UA_AUTOMATION_ENTITY_TYPE],
        meta: {
            icon: "icon-enter",
            label: "#actions_move",
        },
    },
    {
        type: "modal",
        alias: "UmbracoAutomate.Modal.AutomationMove",
        name: "Automation Move Modal",
        js: () => import("./modal/automation-move-modal.element.js"),
    },
];
