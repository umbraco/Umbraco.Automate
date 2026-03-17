import {
    UA_CONNECTION_TREE_ALIAS,
    UA_CONNECTION_TREE_REPOSITORY_ALIAS,
    UA_CONNECTION_TREE_STORE_ALIAS,
} from "./constants.js";
import { UA_CONNECTION_ENTITY_TYPE } from "../entity.js";

export const connectionTreeManifests: Array<UmbExtensionManifest> = [
    {
        type: "repository",
        alias: UA_CONNECTION_TREE_REPOSITORY_ALIAS,
        name: "Connection Tree Repository",
        api: () => import("./connection-tree.repository.js"),
    },
    {
        type: "store",
        alias: UA_CONNECTION_TREE_STORE_ALIAS,
        name: "Connection Tree Store",
        api: () => import("./connection-tree.store.js"),
    },
    {
        type: "tree",
        kind: "default",
        alias: UA_CONNECTION_TREE_ALIAS,
        name: "Connection Tree",
        api: () => import("./connection-tree.context.js"),
        element: () => import("./connection-tree.element.js"),
        meta: {
            repositoryAlias: UA_CONNECTION_TREE_REPOSITORY_ALIAS,
        },
    },
    {
        type: "treeItem",
        alias: "UmbracoAutomate.TreeItem.Connection",
        kind: "default",
        name: "Connection Tree Item",
        forEntityTypes: [UA_CONNECTION_ENTITY_TYPE],
    },
];
