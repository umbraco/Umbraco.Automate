import { UA_AUTOMATION_TREE_ITEM_CHILDREN_COLLECTION_REPOSITORY_ALIAS } from "./constants.js";
import type { ManifestRepository } from "@umbraco-cms/backoffice/extension-registry";

const repository: ManifestRepository = {
    type: "repository",
    alias: UA_AUTOMATION_TREE_ITEM_CHILDREN_COLLECTION_REPOSITORY_ALIAS,
    name: "Automation Tree Item Children Collection Repository",
    api: () => import("./automation-tree-item-children-collection.repository.js"),
};

export const manifests = [repository];
