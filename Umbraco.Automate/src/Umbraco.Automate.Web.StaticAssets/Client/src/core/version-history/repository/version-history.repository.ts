import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { tryExecute } from "@umbraco-cms/backoffice/resources";
import type { UaVersionHistoryResponse, UaVersionComparisonResponse } from "../types.js";
import { UaVersionHistoryTypeMapper } from "../type-mapper.js";
import { VersionHistoryService } from "../../../api/index.js";

const DEFAULT_ENTITY_TYPE = "automation";

/**
 * Repository for version history operations.
 */
export class UaVersionHistoryRepository {
    #host: UmbControllerHost;
    #entityType: string;

    constructor(host: UmbControllerHost, entityType: string = DEFAULT_ENTITY_TYPE) {
        this.#host = host;
        this.#entityType = entityType;
    }

    /**
     * Gets the version history for an entity.
     * @param entityId - The entity ID.
     * @param skip - Number of versions to skip (for pagination).
     * @param take - Number of versions to return.
     * @returns The version history response.
     */
    async getVersionHistory(
        entityId: string,
        skip: number,
        take: number,
    ): Promise<UaVersionHistoryResponse | undefined> {
        const { data, error } = await tryExecute(
            this.#host,
            VersionHistoryService.getVersionHistoryByEntityTypeByEntityId({
                path: { entityType: this.#entityType, entityId },
                query: { skip, take },
            }),
        );

        if (error || !data) {
            console.error("Failed to load version history:", error);
            return undefined;
        }

        return UaVersionHistoryTypeMapper.mapToVersionHistoryResponse(data);
    }

    /**
     * Compares two versions of an entity.
     * @param entityId - The entity ID.
     * @param fromVersion - The source version number.
     * @param toVersion - The target version number.
     * @returns The comparison response with property changes.
     */
    async compareVersions(
        entityId: string,
        fromVersion: number,
        toVersion: number,
    ): Promise<UaVersionComparisonResponse | undefined> {
        const { data, error } = await tryExecute(
            this.#host,
            VersionHistoryService.getVersionHistoryByEntityTypeByEntityIdByFromEntityVersionCompareByToEntityVersion({
                path: {
                    entityType: this.#entityType,
                    entityId,
                    fromEntityVersion: fromVersion,
                    toEntityVersion: toVersion,
                },
            }),
        );

        if (error || !data) {
            console.error("Failed to compare versions:", error);
            return undefined;
        }

        return UaVersionHistoryTypeMapper.mapToComparisonResponse(data);
    }

    /**
     * Rolls back an entity to a previous version.
     * @param entityId - The entity ID.
     * @param version - The version number to rollback to.
     * @returns True if rollback was successful.
     */
    async rollback(entityId: string, version: number): Promise<boolean> {
        const { error } = await tryExecute(
            this.#host,
            VersionHistoryService.postVersionHistoryByEntityTypeByEntityIdByEntityVersionRollback({
                path: {
                    entityType: this.#entityType,
                    entityId,
                    entityVersion: version,
                },
            }),
        );

        if (error) {
            console.error("Failed to rollback:", error);
            return false;
        }

        return true;
    }
}
