import {
    UA_AUTOMATION_GROUP_WORKSPACE_ALIAS,
    UA_AUTOMATION_GROUP_ENTITY_TYPE,
} from "../../constants.js";

export const manifests: Array<UmbExtensionManifest> = [
    {
        type: "workspace",
        kind: "routable",
        alias: UA_AUTOMATION_GROUP_WORKSPACE_ALIAS,
        name: "Automation Group Workspace",
        api: () => import("./automation-group-workspace.context.js"),
        meta: {
            entityType: UA_AUTOMATION_GROUP_ENTITY_TYPE,
        },
    },
];
