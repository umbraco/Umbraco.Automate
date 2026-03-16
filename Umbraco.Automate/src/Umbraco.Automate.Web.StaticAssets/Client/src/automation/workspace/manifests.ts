import { manifests as automationRootManifests } from "./automation-root/manifests.js";
import { manifests as automationGroupManifests } from "./automation-group/manifests.js";
import { manifests as automationManifests } from "./automation/manifests.js";

export const automationWorkspaceManifests: Array<UmbExtensionManifest> = [
    ...automationRootManifests,
    ...automationGroupManifests,
    ...automationManifests,
];
