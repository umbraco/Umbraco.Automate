import {
    UA_AUTOMATION_ENTITY_TYPE,
    UA_AUTOMATION_GROUP_ENTITY_TYPE,
    UA_AUTOMATION_ROOT_ENTITY_TYPE,
} from "../constants.js";
import { UA_WORKSPACE_ENTITY_TYPE } from "../../workspace-management/constants.js";
import { UA_AUTOMATION_FOLDER_REPOSITORY_ALIAS } from "../tree/folder/constants.js";
import { automationMoveManifests } from "./move/manifests.js";
import { UA_ENTITY_AUTOMATION_HAS_MANUAL_TRIGGER_CONDITION_ALIAS } from "./automation-has-manual-trigger.condition.js";

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
    // Run now — only surfaces for automations that use the Manual trigger.
    {
        type: "entityAction",
        kind: "default",
        alias: "UmbracoAutomate.EntityAction.Automation.RunNow",
        name: "Run Now Automation Entity Action",
        weight: 300,
        api: () => import("./automation-run-now.action.js"),
        forEntityTypes: [UA_AUTOMATION_ENTITY_TYPE],
        meta: {
            icon: "icon-play",
            label: "#uaAutomation_runNow",
        },
        conditions: [
            { alias: UA_ENTITY_AUTOMATION_HAS_MANUAL_TRIGGER_CONDITION_ALIAS },
        ],
    },
    {
        type: "condition",
        alias: UA_ENTITY_AUTOMATION_HAS_MANUAL_TRIGGER_CONDITION_ALIAS,
        name: "Entity Automation Has Manual Trigger Condition",
        api: () => import("./automation-has-manual-trigger.condition.js"),
    },
    // Export automation as JSON
    {
        type: "entityAction",
        kind: "default",
        alias: "UmbracoAutomate.EntityAction.Automation.Export",
        name: "Export Automation Entity Action",
        weight: 100,
        api: () => import("./automation-export.action.js"),
        forEntityTypes: [UA_AUTOMATION_ENTITY_TYPE],
        meta: {
            icon: "icon-download-alt",
            label: "#uaGeneral_export",
        },
    },
    // Delete automation — destructive, placed at the bottom of the menu
    {
        type: "entityAction",
        kind: "default",
        alias: "UmbracoAutomate.EntityAction.Automation.Delete",
        name: "Delete Automation Entity Action",
        weight: 10,
        api: () => import("./automation-delete.action.js"),
        forEntityTypes: [UA_AUTOMATION_ENTITY_TYPE],
        meta: {
            icon: "icon-trash",
            label: "#actions_delete",
        },
    },
    ...automationMoveManifests,
];
