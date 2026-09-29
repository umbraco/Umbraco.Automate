import { UA_WORKSPACE_COLLECTION_ALIAS } from "../../constants.js";

export const workspaceCollectionActionManifests: Array<UmbExtensionManifest> = [
    {
        type: "collectionAction",
        // The "button" kind supplies the no-op api that umb-extension-with-api-slot requires.
        // Without it the extension is skipped entirely and no create button renders, because
        // that slot needs both an element and an api. Our own element still takes precedence.
        kind: "button",
        alias: "UmbracoAutomate.CollectionAction.Workspace.Create",
        name: "Create Workspace",
        element: () => import("./workspace-create-collection-action.element.js"),
        conditions: [{ alias: "Umb.Condition.CollectionAlias", match: UA_WORKSPACE_COLLECTION_ALIAS }],
    },
];
