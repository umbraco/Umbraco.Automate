import { connectionCollectionManifests } from "./collection/manifests.js";
import { connectionEntityActionManifests } from "./entity-actions/manifests.js";
import { connectionMenuManifests } from "./menu/manifests.js";
import { connectionRepositoryManifests } from "./repository/manifests.js";
import { connectionTreeManifests } from "./tree/manifests.js";
import { connectionWorkspaceManifests } from "./workspace/manifests.js";

export const connectionManifests = [
    ...connectionCollectionManifests,
    ...connectionEntityActionManifests,
    ...connectionMenuManifests,
    ...connectionRepositoryManifests,
    ...connectionTreeManifests,
    ...connectionWorkspaceManifests,
];
