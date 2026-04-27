import { manifests as actionManifests } from "./action/manifests.js";
import { manifests as repositoryManifests } from "./repository/manifests.js";
import { manifests as viewManifests } from "./views/manifests.js";
import { UA_AUTOMATION_TREE_ITEM_CHILDREN_COLLECTION_ALIAS } from "./constants.js";
import { UA_AUTOMATION_TREE_ITEM_CHILDREN_COLLECTION_REPOSITORY_ALIAS } from "./repository/constants.js";
import type { ManifestCollection } from "@umbraco-cms/backoffice/collection";

const collection: ManifestCollection = {
    type: "collection",
    kind: "default",
    alias: UA_AUTOMATION_TREE_ITEM_CHILDREN_COLLECTION_ALIAS,
    name: "Automation Tree Item Children Collection",
    element: () => import("./automation-tree-item-children-collection.element.js"),
    meta: {
        repositoryAlias: UA_AUTOMATION_TREE_ITEM_CHILDREN_COLLECTION_REPOSITORY_ALIAS,
    },
};

export const manifests = [collection, ...actionManifests, ...repositoryManifests, ...viewManifests];
