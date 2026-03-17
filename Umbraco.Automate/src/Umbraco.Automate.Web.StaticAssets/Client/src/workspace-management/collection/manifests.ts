import { UA_WORKSPACE_COLLECTION_ALIAS } from "./constants.js";
import { UA_WORKSPACE_COLLECTION_REPOSITORY_ALIAS } from "../repository/constants.js";
import { workspaceCollectionActionManifests } from "./action/manifests.js";
import { workspaceBulkActionManifests } from "./bulk-action/manifests.js";

export const workspaceManagementCollectionManifests: Array<UmbExtensionManifest> = [
    {
        type: "collection",
        kind: "default",
        alias: UA_WORKSPACE_COLLECTION_ALIAS,
        name: "Workspace Collection",
        element: () => import("./workspace-collection.element.js"),
        meta: {
            repositoryAlias: UA_WORKSPACE_COLLECTION_REPOSITORY_ALIAS,
        },
    },
    {
        type: "collectionView",
        alias: "UmbracoAutomate.CollectionView.Workspace.Table",
        name: "Workspace Table View",
        element: () => import("./views/table/workspace-table-collection-view.element.js"),
        meta: {
            label: "Table",
            icon: "icon-list",
            pathName: "table",
        },
        conditions: [{ alias: "Umb.Condition.CollectionAlias", match: UA_WORKSPACE_COLLECTION_ALIAS }],
    },
    ...workspaceCollectionActionManifests,
    ...workspaceBulkActionManifests,
];
