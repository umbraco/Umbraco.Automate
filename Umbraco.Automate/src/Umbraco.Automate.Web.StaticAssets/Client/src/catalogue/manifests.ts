import { manifests as modalManifests } from "./modals/manifests.js";
import { UA_CATALOGUE_REPOSITORY_ALIAS } from "./constants.js";

export const catalogueManifests: Array<UmbExtensionManifest> = [
    {
        type: "repository",
        alias: UA_CATALOGUE_REPOSITORY_ALIAS,
        name: "Catalogue Repository",
        api: () => import("./repository/catalogue.repository.js"),
    },
    ...modalManifests,
];
