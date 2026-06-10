import {
    UA_WORKSPACE_VIEW_WORKSPACE_ALIAS,
    UA_WORKSPACE_ENTITY_TYPE,
} from "../../constants.js";

export const manifests: Array<UmbExtensionManifest> = [
    {
        type: "workspace",
        kind: "routable",
        alias: UA_WORKSPACE_VIEW_WORKSPACE_ALIAS,
        name: "Workspace View Workspace",
        api: () => import("./workspace-view-workspace.context.js"),
        meta: {
            entityType: UA_WORKSPACE_ENTITY_TYPE,
        },
    },
];
