import { manifests as connectionRootManifests } from "./connection-root/manifests.js";
import { manifests as connectionManifests } from "./connection/manifests.js";

export const connectionWorkspaceManifests: Array<UmbExtensionManifest> = [
    ...connectionRootManifests,
    ...connectionManifests,
];
