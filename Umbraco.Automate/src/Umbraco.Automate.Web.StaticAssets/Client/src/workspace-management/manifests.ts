import { workspaceManagementCollectionManifests } from "./collection/manifests.js";
import { workspaceManagementEntityActionManifests } from "./entity-actions/manifests.js";
import { workspaceManagementMenuManifests } from "./menu/manifests.js";
import { workspaceManagementRepositoryManifests } from "./repository/manifests.js";
import { workspaceManagementTreeManifests } from "./tree/manifests.js";
import { workspaceMgmtTreeManifests } from "./tree-mgmt/manifests.js";
import { workspaceManagementWorkspaceManifests } from "./workspace/manifests.js";

export const workspaceManagementManifests = [
    ...workspaceManagementCollectionManifests,
    ...workspaceManagementEntityActionManifests,
    ...workspaceManagementMenuManifests,
    ...workspaceManagementRepositoryManifests,
    ...workspaceManagementTreeManifests,
    ...workspaceMgmtTreeManifests,
    ...workspaceManagementWorkspaceManifests,
];
