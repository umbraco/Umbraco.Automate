import {
    UA_CONNECTION_ROOT_WORKSPACE_ALIAS,
    UA_CONNECTION_ROOT_ENTITY_TYPE,
    UA_CONNECTION_ICON,
    UA_CONNECTION_COLLECTION_ALIAS,
} from "../../constants.js";
import { UMB_WORKSPACE_CONDITION_ALIAS } from "@umbraco-cms/backoffice/workspace";

export const manifests: Array<UmbExtensionManifest> = [
    {
        type: "workspace",
        kind: "default",
        alias: UA_CONNECTION_ROOT_WORKSPACE_ALIAS,
        name: "Connection Root Workspace",
        meta: {
            entityType: UA_CONNECTION_ROOT_ENTITY_TYPE,
            headline: "#uaMenu_connections",
        },
    },
    {
        type: "workspaceView",
        kind: "collection",
        alias: "UmbracoAutomate.WorkspaceView.ConnectionRoot.Collection",
        name: "Connection Root Collection Workspace View",
        meta: {
            label: "Collection",
            pathname: "collection",
            icon: UA_CONNECTION_ICON,
            collectionAlias: UA_CONNECTION_COLLECTION_ALIAS,
        },
        conditions: [
            {
                alias: UMB_WORKSPACE_CONDITION_ALIAS,
                match: UA_CONNECTION_ROOT_WORKSPACE_ALIAS,
            },
        ],
    },
];
