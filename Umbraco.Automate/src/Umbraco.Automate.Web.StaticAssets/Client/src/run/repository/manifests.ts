import type { ManifestRepository } from "@umbraco-cms/backoffice/extension-registry";
import {
    UA_RUN_DETAIL_REPOSITORY_ALIAS,
    UA_RUN_COLLECTION_REPOSITORY_ALIAS,
} from "./constants.js";

export const runRepositoryManifests: Array<ManifestRepository> = [
    {
        type: "repository",
        alias: UA_RUN_DETAIL_REPOSITORY_ALIAS,
        name: "Run Detail Repository",
        api: () => import("./detail/run-detail.repository.js"),
    },
    {
        type: "repository",
        alias: UA_RUN_COLLECTION_REPOSITORY_ALIAS,
        name: "Run Collection Repository",
        api: () => import("./collection/run-collection.repository.js"),
    },
];
