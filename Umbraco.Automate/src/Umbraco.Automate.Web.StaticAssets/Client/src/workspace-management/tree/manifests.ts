import {
    UA_WORKSPACE_TREE_ALIAS,
    UA_WORKSPACE_TREE_REPOSITORY_ALIAS,
    UA_WORKSPACE_TREE_STORE_ALIAS,
} from "./constants.js";
import { UA_WORKSPACE_ENTITY_TYPE } from "../entity.js";

export const workspaceManagementTreeManifests: Array<UmbExtensionManifest> = [
    {
        type: "repository",
        alias: UA_WORKSPACE_TREE_REPOSITORY_ALIAS,
        name: "Workspace Tree Repository",
        api: () => import("./workspace-tree.repository.js"),
    },
    {
        type: "store",
        alias: UA_WORKSPACE_TREE_STORE_ALIAS,
        name: "Workspace Tree Store",
        api: () => import("./workspace-tree.store.js"),
    },
    {
        type: "tree",
        kind: "default",
        alias: UA_WORKSPACE_TREE_ALIAS,
        name: "Workspace Tree",
        api: () => import("./workspace-tree.context.js"),
        element: () => import("./workspace-tree.element.js"),
        meta: {
            repositoryAlias: UA_WORKSPACE_TREE_REPOSITORY_ALIAS,
        },
    },
    {
        type: "treeItem",
        alias: "UmbracoAutomate.TreeItem.Workspace",
        name: "Workspace Tree Item",
        forEntityTypes: [UA_WORKSPACE_ENTITY_TYPE],
        api: () => import("./tree-item/workspace-tree-item.context.js"),
        element: () => import("./tree-item/workspace-tree-item.element.js"),
    },
];
