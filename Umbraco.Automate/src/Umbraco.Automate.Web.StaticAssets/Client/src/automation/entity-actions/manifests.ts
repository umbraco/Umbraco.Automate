import { UA_AUTOMATION_ENTITY_TYPE, UA_AUTOMATION_ROOT_ENTITY_TYPE } from "../constants.js";

export const automationEntityActionManifests: Array<UmbExtensionManifest> = [
    {
        type: "entityAction",
        kind: "default",
        alias: "UmbracoAutomate.EntityAction.Automation.Create",
        name: "Create Automation Entity Action",
        weight: 1200,
        api: () => import("./automation-create.action.js"),
        forEntityTypes: [UA_AUTOMATION_ROOT_ENTITY_TYPE],
        meta: {
            icon: "icon-add",
            label: "#uaGeneral_create",
        },
    },
    {
        type: "entityAction",
        kind: "default",
        alias: "UmbracoAutomate.EntityAction.Automation.Delete",
        name: "Delete Automation Entity Action",
        weight: 100,
        api: () => import("./automation-delete.action.js"),
        forEntityTypes: [UA_AUTOMATION_ENTITY_TYPE],
        meta: {
            icon: "icon-trash",
            label: "#actions_delete",
        },
    },
];
