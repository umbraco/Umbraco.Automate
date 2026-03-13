import {
    UA_WORKSPACE_ROOT_WORKSPACE_ALIAS,
    UA_WORKSPACE_ROOT_ENTITY_TYPE,
    UA_WORKSPACE_ICON,
    UA_WORKSPACE_COLLECTION_ALIAS,
} from "../../constants.js";
import { UMB_WORKSPACE_CONDITION_ALIAS } from "@umbraco-cms/backoffice/workspace";

export const manifests: Array<UmbExtensionManifest> = [
    {
        type: "workspace",
        kind: "default",
        alias: UA_WORKSPACE_ROOT_WORKSPACE_ALIAS,
        name: "Workspace Root Workspace",
        meta: {
            entityType: UA_WORKSPACE_ROOT_ENTITY_TYPE,
            headline: "#uaSections_automate",
        },
    },
    {
        type: "workspaceView",
        kind: "collection",
        alias: "UmbracoAutomate.WorkspaceView.WorkspaceRoot.Collection",
        name: "Workspace Root Collection Workspace View",
        meta: {
            label: "Collection",
            pathname: "collection",
            icon: UA_WORKSPACE_ICON,
            collectionAlias: UA_WORKSPACE_COLLECTION_ALIAS,
        },
        conditions: [
            {
                alias: UMB_WORKSPACE_CONDITION_ALIAS,
                match: UA_WORKSPACE_ROOT_WORKSPACE_ALIAS,
            },
        ],
    },
];
