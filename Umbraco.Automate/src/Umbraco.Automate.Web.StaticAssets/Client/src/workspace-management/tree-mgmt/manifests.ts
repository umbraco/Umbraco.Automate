import {
    UA_WORKSPACE_MGMT_TREE_ALIAS,
    UA_WORKSPACE_MGMT_TREE_REPOSITORY_ALIAS,
    UA_WORKSPACE_MGMT_TREE_STORE_ALIAS,
} from "./constants.js";
import { UA_WORKSPACE_MGMT_ENTITY_TYPE } from "../entity.js";

export const workspaceMgmtTreeManifests: Array<UmbExtensionManifest> = [
    {
        type: "repository",
        alias: UA_WORKSPACE_MGMT_TREE_REPOSITORY_ALIAS,
        name: "Workspace Mgmt Tree Repository",
        api: () => import("./workspace-mgmt-tree.repository.js"),
    },
    {
        type: "store",
        alias: UA_WORKSPACE_MGMT_TREE_STORE_ALIAS,
        name: "Workspace Mgmt Tree Store",
        api: () => import("./workspace-mgmt-tree.store.js"),
    },
    {
        type: "tree",
        kind: "default",
        alias: UA_WORKSPACE_MGMT_TREE_ALIAS,
        name: "Workspace Mgmt Tree",
        api: () => import("./workspace-mgmt-tree.context.js"),
        element: () => import("./workspace-mgmt-tree.element.js"),
        meta: {
            repositoryAlias: UA_WORKSPACE_MGMT_TREE_REPOSITORY_ALIAS,
        },
    },
    {
        type: "treeItem",
        kind: "default",
        alias: "UmbracoAutomate.TreeItem.WorkspaceMgmt",
        name: "Workspace Mgmt Tree Item",
        forEntityTypes: [UA_WORKSPACE_MGMT_ENTITY_TYPE],
    },
];
