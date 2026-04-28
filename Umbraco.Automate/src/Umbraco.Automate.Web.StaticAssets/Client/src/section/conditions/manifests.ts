import { UA_WORKSPACES_EXIST_CONDITION_ALIAS } from "./workspaces-exist.condition.constants.js";

export const conditionManifests: UmbExtensionManifest[] = [
    {
        type: "condition",
        alias: UA_WORKSPACES_EXIST_CONDITION_ALIAS,
        name: "Workspaces Exist Condition",
        api: () => import("./workspaces-exist.condition.js"),
    },
];
