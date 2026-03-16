import { UA_AUTOMATION_TREE_ITEM_CHILDREN_COLLECTION_ALIAS } from "../constants.js";
import { UMB_COLLECTION_ALIAS_CONDITION } from "@umbraco-cms/backoffice/collection";
import type { ManifestCollectionView } from "@umbraco-cms/backoffice/collection";

const tableCollectionView: ManifestCollectionView = {
    type: "collectionView",
    alias: "UmbracoAutomate.CollectionView.Automation.TreeItem.Table",
    name: "Automation Tree Item Table Collection View",
    element: () => import("./automation-tree-item-table-collection-view.element.js"),
    weight: 300,
    meta: {
        label: "Table",
        icon: "icon-list",
        pathName: "table",
    },
    conditions: [
        {
            alias: UMB_COLLECTION_ALIAS_CONDITION,
            match: UA_AUTOMATION_TREE_ITEM_CHILDREN_COLLECTION_ALIAS,
        },
    ],
};

export const manifests = [tableCollectionView];
