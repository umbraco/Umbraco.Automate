import {
    UA_AUTOMATION_ENTITY_TYPE,
    UA_AUTOMATION_GROUP_ENTITY_TYPE,
    UA_AUTOMATION_ROOT_ENTITY_TYPE,
} from "../constants.js";
import { UA_WORKSPACE_ENTITY_TYPE } from "../../workspace-management/constants.js";
import { UA_AUTOMATION_FOLDER_REPOSITORY_ALIAS } from "../tree/folder/constants.js";
import { automationMoveManifests } from "./move/manifests.js";

export const automationEntityActionManifests: Array<UmbExtensionManifest> = [
    // The "create" kind opens the entity-create-option-action-list modal
    // when multiple options are registered, or executes directly for a single option.
    {
        type: "entityAction",
        kind: "create",
        alias: "UmbracoAutomate.EntityAction.Automation.Create",
        name: "Create Automation Entity Action",
        weight: 1200,
        forEntityTypes: [UA_AUTOMATION_ROOT_ENTITY_TYPE, UA_AUTOMATION_GROUP_ENTITY_TYPE, UA_WORKSPACE_ENTITY_TYPE],
    },
    // Option: Create Automation
    {
        type: "entityCreateOptionAction",
        alias: "UmbracoAutomate.EntityCreateOptionAction.Automation",
        name: "Create Automation Option",
        weight: 1000,
        api: () => import("./automation-create-option.action.js"),
        forEntityTypes: [UA_AUTOMATION_ROOT_ENTITY_TYPE, UA_AUTOMATION_GROUP_ENTITY_TYPE, UA_WORKSPACE_ENTITY_TYPE],
        meta: {
            icon: "icon-mindmap",
            label: "#uaGeneral_createAutomation",
        },
    },
    // Option: Create Folder
    {
        type: "entityCreateOptionAction",
        kind: "folder",
        alias: "UmbracoAutomate.EntityCreateOptionAction.Folder",
        name: "Create Automation Folder Option",
        forEntityTypes: [UA_AUTOMATION_ROOT_ENTITY_TYPE, UA_AUTOMATION_GROUP_ENTITY_TYPE, UA_WORKSPACE_ENTITY_TYPE],
        meta: {
            icon: "icon-folder",
            label: "#uaGeneral_createFolder",
            folderRepositoryAlias: UA_AUTOMATION_FOLDER_REPOSITORY_ALIAS,
        } as any,
    },
    // Delete automation
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
    // Export automation as JSON
    {
        type: "entityAction",
        kind: "default",
        alias: "UmbracoAutomate.EntityAction.Automation.Export",
        name: "Export Automation Entity Action",
        weight: 50,
        api: () => import("./automation-export.action.js"),
        forEntityTypes: [UA_AUTOMATION_ENTITY_TYPE],
        meta: {
            icon: "icon-download-alt",
            label: "#uaGeneral_export",
        },
    },
    ...automationMoveManifests,
];
