/**
 * Bundle Entry Point
 *
 * This file is the entry point for Umbraco's bundle loader.
 * It ONLY exports the manifests array - nothing else.
 *
 * IMPORTANT: Umbraco's bundle loader iterates over ALL exports looking for
 * manifest-like structures (objects with a `type` property). Exporting anything
 * else (class instances, context tokens, singletons) will cause errors.
 */

import { sectionManifests } from "./section/manifests.js";
import { manifests as langManifests } from "./lang/manifests.js";
import { manifests as coreManifests } from "./core/manifests.js";
import { automationManifests } from "./automation/manifests.js";
import { catalogueManifests } from "./catalogue/manifests.js";
import { runManifests } from "./run/manifests.js";
import { workspaceManagementManifests } from "./workspace-management/manifests.js";
import { connectionManifests } from "./connection/manifests.js";
import { approvalManifests } from "./approval/manifests.js";

export const manifests: Array<UmbExtensionManifest> = [
    ...sectionManifests,
    ...langManifests,
    ...coreManifests,
    ...automationManifests,
    ...catalogueManifests,
    ...runManifests,
    ...workspaceManagementManifests,
    ...connectionManifests,
    ...approvalManifests,
];
