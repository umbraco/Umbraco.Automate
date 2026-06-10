import { runDashboardManifests } from "./dashboard/manifests.js";
import { manifests as runModalManifests } from "./modals/manifests.js";
import { runRepositoryManifests } from "./repository/manifests.js";
import { runWorkspaceManifests } from "./workspace/manifests.js";

export const runManifests = [
    ...runDashboardManifests,
    ...runModalManifests,
    ...runRepositoryManifests,
    ...runWorkspaceManifests,
];
