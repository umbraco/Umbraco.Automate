import { manifests as workspaceRootManifests } from "./workspace-root/manifests.js";
import { manifests as workspaceMgmtManifests } from "./workspace-mgmt/manifests.js";

export const workspaceManagementWorkspaceManifests: Array<UmbExtensionManifest> = [
    ...workspaceRootManifests,
    ...workspaceMgmtManifests,
];
