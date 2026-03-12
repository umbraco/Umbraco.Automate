import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { tryExecute } from "@umbraco-cms/backoffice/resources";
import type { UaVersionHistoryResponse, UaVersionComparisonResponse } from "../types.js";
import { UaVersionHistoryTypeMapper } from "../type-mapper.js";
import { AutomationsService } from "../../../api/index.js";

/**
 * Repository for version history operations on automations.
 *
 * NOTE: The version history API endpoints (getAutomationsByIdVersions,
 * getAutomationsByIdVersionsCompare, postAutomationsByIdVersionsRollback)
 * will be available after the backend endpoints are added and the OpenAPI
 * client is regenerated. Until then, these methods will fail gracefully.
 */
export class UaVersionHistoryRepository {
    #host: UmbControllerHost;

    constructor(host: UmbControllerHost) {
        this.#host = host;
    }

    /**
     * Gets the version history for an automation.
     * @param entityId - The automation ID.
     * @param skip - Number of versions to skip (for pagination).
     * @param take - Number of versions to return.
     * @returns The version history response.
     */
    async getVersionHistory(
        entityId: string,
        skip: number,
        take: number,
    ): Promise<UaVersionHistoryResponse | undefined> {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const service = AutomationsService as any;
        if (!service.getAutomationsByIdVersions) {
            console.warn("Version history API not yet available. Regenerate the OpenAPI client after adding the backend endpoint.");
            return undefined;
        }

        const result = await tryExecute(
            this.#host,
            service.getAutomationsByIdVersions({
                path: { id: entityId },
                query: { skip, take },
            }),
            // eslint-disable-next-line @typescript-eslint/no-explicit-any
        ) as any;

        if (result.error || !result.data) {
            console.error("Failed to load automation version history:", result.error);
            return undefined;
        }

        return UaVersionHistoryTypeMapper.mapToVersionHistoryResponse(result.data);
    }

    /**
     * Compares two versions of an automation.
     * @param entityId - The automation ID.
     * @param fromVersion - The source version number.
     * @param toVersion - The target version number.
     * @returns The comparison response with property changes.
     */
    async compareVersions(
        entityId: string,
        fromVersion: number,
        toVersion: number,
    ): Promise<UaVersionComparisonResponse | undefined> {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const service = AutomationsService as any;
        if (!service.getAutomationsByIdVersionsCompare) {
            console.warn("Version comparison API not yet available.");
            return undefined;
        }

        const result = await tryExecute(
            this.#host,
            service.getAutomationsByIdVersionsCompare({
                path: { id: entityId },
                query: { fromVersion, toVersion },
            }),
            // eslint-disable-next-line @typescript-eslint/no-explicit-any
        ) as any;

        if (result.error || !result.data) {
            console.error("Failed to compare automation versions:", result.error);
            return undefined;
        }

        return UaVersionHistoryTypeMapper.mapToComparisonResponse(result.data);
    }

    /**
     * Rolls back an automation to a previous version.
     * @param entityId - The automation ID.
     * @param version - The version number to rollback to.
     * @returns True if rollback was successful.
     */
    async rollback(entityId: string, version: number): Promise<boolean> {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const service = AutomationsService as any;
        if (!service.postAutomationsByIdVersionsRollback) {
            console.warn("Version rollback API not yet available.");
            return false;
        }

        const result = await tryExecute(
            this.#host,
            service.postAutomationsByIdVersionsRollback({
                path: { id: entityId },
                query: { version },
            }),
            // eslint-disable-next-line @typescript-eslint/no-explicit-any
        ) as any;

        if (result.error) {
            console.error("Failed to rollback automation:", result.error);
            return false;
        }

        return true;
    }
}
