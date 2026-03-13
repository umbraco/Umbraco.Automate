import { UA_WORKSPACE_ENTITY_TYPE, UA_WORKSPACE_ROOT_ENTITY_TYPE } from "../constants.js";

export const workspaceManagementEntityActionManifests: Array<UmbExtensionManifest> = [
    {
        type: "entityAction",
        kind: "default",
        alias: "UmbracoAutomate.EntityAction.Workspace.Create",
        name: "Create Workspace Entity Action",
        weight: 1200,
        api: () => import("./workspace-create.action.js"),
        forEntityTypes: [UA_WORKSPACE_ROOT_ENTITY_TYPE],
        meta: {
            icon: "icon-add",
            label: "#uaGeneral_create",
        },
    },
    {
        type: "entityAction",
        kind: "default",
        alias: "UmbracoAutomate.EntityAction.Workspace.Delete",
        name: "Delete Workspace Entity Action",
        weight: 100,
        api: () => import("./workspace-delete.action.js"),
        forEntityTypes: [UA_WORKSPACE_ENTITY_TYPE],
        meta: {
            icon: "icon-trash",
            label: "#actions_delete",
        },
    },
];
