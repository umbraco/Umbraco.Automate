import {
    UA_AUTOMATION_WORKSPACE_ROOT_WORKSPACE_ALIAS,
    UA_AUTOMATION_ICON,
    UA_AUTOMATION_COLLECTION_ALIAS,
    UA_AUTOMATION_WORKSPACE_ENTITY_TYPE,
} from "../../constants.js";
import { UMB_WORKSPACE_CONDITION_ALIAS } from "@umbraco-cms/backoffice/workspace";

export const manifests: Array<UmbExtensionManifest> = [
    {
        type: "workspace",
        kind: "default",
        alias: UA_AUTOMATION_WORKSPACE_ROOT_WORKSPACE_ALIAS,
        name: "Automation Workspace Root Workspace",
        meta: {
            entityType: UA_AUTOMATION_WORKSPACE_ENTITY_TYPE,
            headline: "#uaMenu_automations",
        },
    },
    {
        type: "workspaceView",
        kind: "collection",
        alias: "UmbracoAutomate.WorkspaceView.AutomationWorkspaceRoot.Collection",
        name: "Automation Workspace Root Collection Workspace View",
        meta: {
            label: "Collection",
            pathname: "collection",
            icon: UA_AUTOMATION_ICON,
            collectionAlias: UA_AUTOMATION_COLLECTION_ALIAS,
        },
        conditions: [
            {
                alias: UMB_WORKSPACE_CONDITION_ALIAS,
                match: UA_AUTOMATION_WORKSPACE_ROOT_WORKSPACE_ALIAS,
            },
        ],
    },
];
