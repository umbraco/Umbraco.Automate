import { UA_AUTOMATION_COLLECTION_ALIAS } from "./constants.js";
import { UA_AUTOMATION_COLLECTION_REPOSITORY_ALIAS } from "../repository/constants.js";
import { automationCollectionActionManifests } from "./action/manifests.js";
import { automationBulkActionManifests } from "./bulk-action/manifests.js";

export const automationCollectionManifests: Array<UmbExtensionManifest> = [
    {
        type: "collection",
        kind: "default",
        alias: UA_AUTOMATION_COLLECTION_ALIAS,
        name: "Automation Collection",
        element: () => import("./automation-collection.element.js"),
        meta: {
            repositoryAlias: UA_AUTOMATION_COLLECTION_REPOSITORY_ALIAS,
        },
    },
    {
        type: "collectionView",
        alias: "UmbracoAutomate.CollectionView.Automation.Table",
        name: "Automation Table View",
        element: () => import("./views/table/automation-table-collection-view.element.js"),
        meta: {
            label: "Table",
            icon: "icon-list",
            pathName: "table",
        },
        conditions: [{ alias: "Umb.Condition.CollectionAlias", match: UA_AUTOMATION_COLLECTION_ALIAS }],
    },
    ...automationCollectionActionManifests,
    ...automationBulkActionManifests,
];
