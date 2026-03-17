import { runDashboardManifests } from "./dashboard/manifests.js";
import { runRepositoryManifests } from "./repository/manifests.js";
import { runWorkspaceManifests } from "./workspace/manifests.js";

export const runManifests = [
    ...runDashboardManifests,
    ...runRepositoryManifests,
    ...runWorkspaceManifests,
];
