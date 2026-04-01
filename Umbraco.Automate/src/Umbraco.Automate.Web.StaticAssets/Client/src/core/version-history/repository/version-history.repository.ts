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

    async getVersionHistory(
        entityId: string,
        skip: number,
        take: number,
    ): Promise<{ data?: UaVersionHistoryResponse; error?: unknown }> {
        const { data, error } = await tryExecute(
            this.#host,
            VersionHistoryService.getVersionHistoryByEntityTypeByEntityId({
                path: { entityType: this.#entityType, entityId },
                query: { skip, take },
            }),
        );

        if (error || !data) {
            return { error };
        }

        return { data: UaVersionHistoryTypeMapper.mapToVersionHistoryResponse(data) };
    }

    async compareVersions(
        entityId: string,
        fromVersion: number,
        toVersion: number,
    ): Promise<{ data?: UaVersionComparisonResponse; error?: unknown }> {
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
            return { error };
        }

        return { data: UaVersionHistoryTypeMapper.mapToComparisonResponse(data) };
    }

    async rollback(entityId: string, version: number): Promise<{ error?: unknown }> {
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
            return { error };
        }

        return {};
    }
}
