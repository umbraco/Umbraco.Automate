import { UA_AUTOMATION_COLLECTION_ALIAS } from "../constants.js";
import { UA_AUTOMATION_ENTITY_TYPE } from "../../constants.js";
import { UMB_COLLECTION_ALIAS_CONDITION } from "@umbraco-cms/backoffice/collection";

export const automationBulkActionManifests: Array<UmbExtensionManifest> = [
    {
        type: "entityBulkAction",
        kind: "default",
        alias: "UmbracoAutomate.EntityBulkAction.Automation.Delete",
        name: "Delete Automations Bulk Action",
        weight: 100,
        api: () => import("./automation-bulk-delete.action.js"),
        forEntityTypes: [UA_AUTOMATION_ENTITY_TYPE],
        meta: {
            icon: "icon-trash",
            label: "#actions_delete",
        },
        conditions: [
            {
                alias: UMB_COLLECTION_ALIAS_CONDITION,
                match: UA_AUTOMATION_COLLECTION_ALIAS,
            },
        ],
    },
];
