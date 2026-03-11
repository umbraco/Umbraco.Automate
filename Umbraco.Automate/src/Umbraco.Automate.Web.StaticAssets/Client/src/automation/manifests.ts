import { automationCollectionManifests } from "./collection/manifests.js";
import { automationEntityActionManifests } from "./entity-actions/manifests.js";
import { automationMenuManifests } from "./menu/manifests.js";
import { automationModalManifests } from "./modals/manifests.js";
import { automationRepositoryManifests } from "./repository/manifests.js";
import { automationTreeManifests } from "./tree/manifests.js";
import { automationWorkspaceManifests } from "./workspace/manifests.js";

export const automationManifests = [
    ...automationCollectionManifests,
    ...automationEntityActionManifests,
    ...automationMenuManifests,
    ...automationModalManifests,
    ...automationRepositoryManifests,
    ...automationTreeManifests,
    ...automationWorkspaceManifests,
];
