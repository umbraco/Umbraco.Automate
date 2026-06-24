import { automationCollectionManifests } from "./collection/manifests.js";
import { automationEntityActionManifests } from "./entity-actions/manifests.js";
import { automationMenuManifests } from "./menu/manifests.js";
import { automationModalManifests } from "./modals/manifests.js";
import { automationRepositoryManifests } from "./repository/manifests.js";
import { automationSearchManifests } from "./search/manifests.js";
import { automationFolderManifests } from "./tree/folder/manifests.js";
import { automationTreeManifests } from "./tree/manifests.js";
import { automationWorkspaceManifests } from "./workspace/manifests.js";

export const automationManifests = [
    ...automationCollectionManifests,
    ...automationEntityActionManifests,
    ...automationFolderManifests,
    ...automationMenuManifests,
    ...automationModalManifests,
    ...automationRepositoryManifests,
    ...automationSearchManifests,
    ...automationTreeManifests,
    ...automationWorkspaceManifests,
];
