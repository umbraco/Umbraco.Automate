import {
    UA_AUTOMATION_TREE_ALIAS,
    UA_AUTOMATION_TREE_REPOSITORY_ALIAS,
    UA_AUTOMATION_TREE_STORE_ALIAS,
} from "./constants.js";
import { UA_AUTOMATION_ENTITY_TYPE } from "../entity.js";

export const automationTreeManifests: Array<UmbExtensionManifest> = [
    {
        type: "repository",
        alias: UA_AUTOMATION_TREE_REPOSITORY_ALIAS,
        name: "Automation Tree Repository",
        api: () => import("./automation-tree.repository.js"),
    },
    {
        type: "store",
        alias: UA_AUTOMATION_TREE_STORE_ALIAS,
        name: "Automation Tree Store",
        api: () => import("./automation-tree.store.js"),
    },
    {
        type: "tree",
        kind: "default",
        alias: UA_AUTOMATION_TREE_ALIAS,
        name: "Automation Tree",
        api: () => import("./automation-tree.context.js"),
        element: () => import("./automation-tree.element.js"),
        meta: {
            repositoryAlias: UA_AUTOMATION_TREE_REPOSITORY_ALIAS,
        },
    },
    {
        type: "treeItem",
        alias: "UmbracoAutomate.TreeItem.Automation",
        name: "Automation Tree Item",
        forEntityTypes: [UA_AUTOMATION_ENTITY_TYPE],
        api: () => import("./tree-item/automation-tree-item.context.js"),
        element: () => import("./tree-item/automation-tree-item.element.js"),
    },
];
