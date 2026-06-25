import {
    UA_WORKSPACE_TREE_ALIAS,
    UA_WORKSPACE_TREE_REPOSITORY_ALIAS,
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
        kind: "default",
        name: "Workspace Tree Item",
        forEntityTypes: [UA_WORKSPACE_ENTITY_TYPE],
    },
];
