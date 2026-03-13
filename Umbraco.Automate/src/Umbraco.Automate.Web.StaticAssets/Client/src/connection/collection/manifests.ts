import { UA_CONNECTION_COLLECTION_ALIAS } from "./constants.js";
import { UA_CONNECTION_COLLECTION_REPOSITORY_ALIAS } from "../repository/constants.js";
import { connectionCollectionActionManifests } from "./action/manifests.js";
import { connectionBulkActionManifests } from "./bulk-action/manifests.js";

export const connectionCollectionManifests: Array<UmbExtensionManifest> = [
    {
        type: "collection",
        kind: "default",
        alias: UA_CONNECTION_COLLECTION_ALIAS,
        name: "Connection Collection",
        element: () => import("./connection-collection.element.js"),
        meta: {
            repositoryAlias: UA_CONNECTION_COLLECTION_REPOSITORY_ALIAS,
        },
    },
    {
        type: "collectionView",
        alias: "UmbracoAutomate.CollectionView.Connection.Table",
        name: "Connection Table View",
        element: () => import("./views/table/connection-table-collection-view.element.js"),
        meta: {
            label: "Table",
            icon: "icon-list",
            pathName: "table",
        },
        conditions: [{ alias: "Umb.Condition.CollectionAlias", match: UA_CONNECTION_COLLECTION_ALIAS }],
    },
    ...connectionCollectionActionManifests,
    ...connectionBulkActionManifests,
];
