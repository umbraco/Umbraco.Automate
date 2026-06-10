import {
    UA_WORKSPACE_ENTITY_TYPE,
    UA_WORKSPACE_MGMT_ENTITY_TYPE,
    UA_WORKSPACE_MGMT_ROOT_ENTITY_TYPE,
} from "../constants.js";

export const workspaceManagementEntityActionManifests: Array<UmbExtensionManifest> = [
    // Create workspace (available on mgmt tree root in Settings)
    {
        type: "entityAction",
        kind: "default",
        alias: "UmbracoAutomate.EntityAction.Workspace.Create",
        name: "Create Workspace Entity Action",
        weight: 1200,
        api: () => import("./workspace-create.action.js"),
        forEntityTypes: [UA_WORKSPACE_MGMT_ROOT_ENTITY_TYPE],
        meta: {
            icon: "icon-add",
            label: "#uaGeneral_create",
        },
    },
    // Delete workspace (available in mgmt tree)
    {
        type: "entityAction",
        kind: "default",
        alias: "UmbracoAutomate.EntityAction.Workspace.Delete",
        name: "Delete Workspace Entity Action",
        weight: 100,
        api: () => import("./workspace-delete.action.js"),
        forEntityTypes: [UA_WORKSPACE_MGMT_ENTITY_TYPE],
        meta: {
            icon: "icon-trash",
            label: "#actions_delete",
        },
    },
    // Import automation into a workspace (available on user-facing workspace entity)
    {
        type: "entityAction",
        kind: "default",
        alias: "UmbracoAutomate.EntityAction.Workspace.ImportAutomation",
        name: "Import Automation Entity Action",
        weight: 500,
        api: () => import("./workspace-import-automation.action.js"),
        forEntityTypes: [UA_WORKSPACE_ENTITY_TYPE],
        meta: {
            icon: "icon-download-alt",
            label: "#uaGeneral_import",
        },
    },
];
