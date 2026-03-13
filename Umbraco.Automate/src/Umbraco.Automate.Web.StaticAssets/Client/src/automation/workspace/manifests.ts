import { manifests as automationRootManifests } from "./automation-root/manifests.js";
import { manifests as automationWorkspaceRootManifests } from "./automation-workspace-root/manifests.js";
import { manifests as automationManifests } from "./automation/manifests.js";

export const automationWorkspaceManifests: Array<UmbExtensionManifest> = [
    ...automationRootManifests,
    ...automationWorkspaceRootManifests,
    ...automationManifests,
];
