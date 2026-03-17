import {
    UA_AUTOMATION_ROOT_WORKSPACE_ALIAS,
    UA_AUTOMATION_ROOT_ENTITY_TYPE,
    UA_AUTOMATION_ICON,
    UA_AUTOMATION_COLLECTION_ALIAS,
} from "../../constants.js";
import { UMB_WORKSPACE_CONDITION_ALIAS } from "@umbraco-cms/backoffice/workspace";

export const manifests: Array<UmbExtensionManifest> = [
    {
        type: "workspace",
        kind: "default",
        alias: UA_AUTOMATION_ROOT_WORKSPACE_ALIAS,
        name: "Automation Root Workspace",
        meta: {
            entityType: UA_AUTOMATION_ROOT_ENTITY_TYPE,
            headline: "#uaSections_automate",
        },
    },
    {
        type: "workspaceView",
        kind: "collection",
        alias: "UmbracoAutomate.WorkspaceView.AutomationRoot.Collection",
        name: "Automation Root Collection Workspace View",
        meta: {
            label: "Collection",
            pathname: "collection",
            icon: UA_AUTOMATION_ICON,
            collectionAlias: UA_AUTOMATION_COLLECTION_ALIAS,
        },
        conditions: [
            {
                alias: UMB_WORKSPACE_CONDITION_ALIAS,
                match: UA_AUTOMATION_ROOT_WORKSPACE_ALIAS,
            },
        ],
    },
];
