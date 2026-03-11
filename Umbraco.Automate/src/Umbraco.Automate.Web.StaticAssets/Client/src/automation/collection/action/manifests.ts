import { UA_AUTOMATION_COLLECTION_ALIAS } from "../../constants.js";

export const automationCollectionActionManifests: Array<UmbExtensionManifest> = [
    {
        type: "collectionAction",
        alias: "UmbracoAutomate.CollectionAction.Automation.Create",
        name: "Create Automation",
        element: () => import("./automation-create-collection-action.element.js"),
        conditions: [{ alias: "Umb.Condition.CollectionAlias", match: UA_AUTOMATION_COLLECTION_ALIAS }],
    },
];
