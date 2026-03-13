import { UA_WORKSPACE_COLLECTION_ALIAS } from "../../constants.js";

export const workspaceCollectionActionManifests: Array<UmbExtensionManifest> = [
    {
        type: "collectionAction",
        alias: "UmbracoAutomate.CollectionAction.Workspace.Create",
        name: "Create Workspace",
        element: () => import("./workspace-create-collection-action.element.js"),
        conditions: [{ alias: "Umb.Condition.CollectionAlias", match: UA_WORKSPACE_COLLECTION_ALIAS }],
    },
];
