import { manifests as workspaceRootManifests } from "./workspace-root/manifests.js";
import { manifests as workspaceMgmtRootManifests } from "./workspace-mgmt-root/manifests.js";
import { manifests as workspaceMgmtManifests } from "./workspace-mgmt/manifests.js";
import { manifests as workspaceViewManifests } from "./workspace-view/manifests.js";

export const workspaceManagementWorkspaceManifests: Array<UmbExtensionManifest> = [
    ...workspaceRootManifests,
    ...workspaceMgmtRootManifests,
    ...workspaceMgmtManifests,
    ...workspaceViewManifests,
];
