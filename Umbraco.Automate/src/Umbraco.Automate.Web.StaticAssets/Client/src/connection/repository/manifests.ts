import type { ManifestRepository, ManifestStore } from "@umbraco-cms/backoffice/extension-registry";
import {
    UA_CONNECTION_DETAIL_REPOSITORY_ALIAS,
    UA_CONNECTION_DETAIL_STORE_ALIAS,
    UA_CONNECTION_COLLECTION_REPOSITORY_ALIAS,
} from "./constants.js";

export const connectionRepositoryManifests: Array<ManifestRepository | ManifestStore> = [
    {
        type: "repository",
        alias: UA_CONNECTION_DETAIL_REPOSITORY_ALIAS,
        name: "Connection Detail Repository",
        api: () => import("./detail/connection-detail.repository.js"),
    },
    {
        type: "store",
        alias: UA_CONNECTION_DETAIL_STORE_ALIAS,
        name: "Connection Detail Store",
        api: () => import("./detail/connection-detail.store.js"),
    },
    {
        type: "repository",
        alias: UA_CONNECTION_COLLECTION_REPOSITORY_ALIAS,
        name: "Connection Collection Repository",
        api: () => import("./collection/connection-collection.repository.js"),
    },
];
