import { UA_CONNECTION_ENTITY_TYPE, UA_CONNECTION_ROOT_ENTITY_TYPE } from "../constants.js";

export const connectionEntityActionManifests: Array<UmbExtensionManifest> = [
    {
        type: "entityAction",
        kind: "default",
        alias: "UmbracoAutomate.EntityAction.Connection.Create",
        name: "Create Connection Entity Action",
        weight: 1200,
        api: () => import("./connection-create.action.js"),
        forEntityTypes: [UA_CONNECTION_ROOT_ENTITY_TYPE],
        meta: {
            icon: "icon-add",
            label: "#uaGeneral_create",
        },
    },
    {
        type: "entityAction",
        kind: "default",
        alias: "UmbracoAutomate.EntityAction.Connection.Delete",
        name: "Delete Connection Entity Action",
        weight: 100,
        api: () => import("./connection-delete.action.js"),
        forEntityTypes: [UA_CONNECTION_ENTITY_TYPE],
        meta: {
            icon: "icon-trash",
            label: "#actions_delete",
        },
    },
];
