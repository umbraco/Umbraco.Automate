import { approvalDashboardManifests } from "./dashboard/manifests.js";
import { approvalModalManifests } from "./modals/manifests.js";

export const approvalManifests = [
    ...approvalDashboardManifests,
    ...approvalModalManifests,
];
