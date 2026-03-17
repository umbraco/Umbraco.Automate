import { manifests as runManifests } from "./run/manifests.js";

export const runWorkspaceManifests: Array<UmbExtensionManifest> = [
    ...runManifests,
];
