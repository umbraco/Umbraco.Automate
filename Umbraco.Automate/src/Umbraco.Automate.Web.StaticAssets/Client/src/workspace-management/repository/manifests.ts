import type { ManifestRepository, ManifestStore } from "@umbraco-cms/backoffice/extension-registry";
import {
    UA_WORKSPACE_DETAIL_REPOSITORY_ALIAS,
    UA_WORKSPACE_DETAIL_STORE_ALIAS,
    UA_WORKSPACE_COLLECTION_REPOSITORY_ALIAS,
} from "./constants.js";

export const workspaceManagementRepositoryManifests: Array<ManifestRepository | ManifestStore> = [
    {
        type: "repository",
        alias: UA_WORKSPACE_DETAIL_REPOSITORY_ALIAS,
        name: "Workspace Detail Repository",
        api: () => import("./detail/workspace-detail.repository.js"),
    },
    {
        type: "store",
        alias: UA_WORKSPACE_DETAIL_STORE_ALIAS,
        name: "Workspace Detail Store",
        api: () => import("./detail/workspace-detail.store.js"),
    },
    {
        type: "repository",
        alias: UA_WORKSPACE_COLLECTION_REPOSITORY_ALIAS,
        name: "Workspace Collection Repository",
        api: () => import("./collection/workspace-collection.repository.js"),
    },
];
