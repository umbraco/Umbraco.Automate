import type { ManifestRepository, ManifestStore } from "@umbraco-cms/backoffice/extension-registry";
import {
    UA_AUTOMATION_DETAIL_REPOSITORY_ALIAS,
    UA_AUTOMATION_DETAIL_STORE_ALIAS,
    UA_AUTOMATION_COLLECTION_REPOSITORY_ALIAS,
} from "./constants.js";

export const automationRepositoryManifests: Array<ManifestRepository | ManifestStore> = [
    {
        type: "repository",
        alias: UA_AUTOMATION_DETAIL_REPOSITORY_ALIAS,
        name: "Automation Detail Repository",
        api: () => import("./detail/automation-detail.repository.js"),
    },
    {
        type: "store",
        alias: UA_AUTOMATION_DETAIL_STORE_ALIAS,
        name: "Automation Detail Store",
        api: () => import("./detail/automation-detail.store.js"),
    },
    {
        type: "repository",
        alias: UA_AUTOMATION_COLLECTION_REPOSITORY_ALIAS,
        name: "Automation Collection Repository",
        api: () => import("./collection/automation-collection.repository.js"),
    },
];
