import { UA_CONNECTION_COLLECTION_ALIAS } from "../constants.js";
import { UA_CONNECTION_ENTITY_TYPE } from "../../constants.js";
import { UMB_COLLECTION_ALIAS_CONDITION } from "@umbraco-cms/backoffice/collection";

export const connectionBulkActionManifests: Array<UmbExtensionManifest> = [
    {
        type: "entityBulkAction",
        kind: "default",
        alias: "UmbracoAutomate.EntityBulkAction.Connection.Delete",
        name: "Delete Connections Bulk Action",
        weight: 100,
        api: () => import("./connection-bulk-delete.action.js"),
        forEntityTypes: [UA_CONNECTION_ENTITY_TYPE],
        meta: {
            icon: "icon-trash",
            label: "#actions_delete",
        },
        conditions: [
            {
                alias: UMB_COLLECTION_ALIAS_CONDITION,
                match: UA_CONNECTION_COLLECTION_ALIAS,
            },
        ],
    },
];
