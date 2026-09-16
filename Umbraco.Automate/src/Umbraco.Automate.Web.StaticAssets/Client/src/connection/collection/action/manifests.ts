import { UA_CONNECTION_COLLECTION_ALIAS } from "../../constants.js";

export const connectionCollectionActionManifests: Array<UmbExtensionManifest> = [
    {
        type: "collectionAction",
        // The "button" kind supplies the no-op api that umb-extension-with-api-slot requires.
        // Without it the extension is skipped entirely and no create button renders, because
        // that slot needs both an element and an api. Our own element still takes precedence.
        kind: "button",
        alias: "UmbracoAutomate.CollectionAction.Connection.Create",
        name: "Create Connection",
        element: () => import("./connection-create-collection-action.element.js"),
        conditions: [{ alias: "Umb.Condition.CollectionAlias", match: UA_CONNECTION_COLLECTION_ALIAS }],
    },
];
