import { UA_WORKSPACE_COLLECTION_ALIAS } from "../constants.js";
import { UA_WORKSPACE_ENTITY_TYPE } from "../../constants.js";
import { UMB_COLLECTION_ALIAS_CONDITION } from "@umbraco-cms/backoffice/collection";

export const workspaceBulkActionManifests: Array<UmbExtensionManifest> = [
    {
        type: "entityBulkAction",
        kind: "default",
        alias: "UmbracoAutomate.EntityBulkAction.Workspace.Delete",
        name: "Delete Workspaces Bulk Action",
        weight: 100,
        api: () => import("./workspace-bulk-delete.action.js"),
        forEntityTypes: [UA_WORKSPACE_ENTITY_TYPE],
        meta: {
            icon: "icon-trash",
            label: "#actions_delete",
        },
        conditions: [
            {
                alias: UMB_COLLECTION_ALIAS_CONDITION,
                match: UA_WORKSPACE_COLLECTION_ALIAS,
            },
        ],
    },
];
