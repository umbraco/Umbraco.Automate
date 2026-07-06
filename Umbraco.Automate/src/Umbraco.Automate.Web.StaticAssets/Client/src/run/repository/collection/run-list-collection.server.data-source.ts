import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { tryExecute } from "@umbraco-cms/backoffice/resources";
import { RunsService } from "../../../api/sdk.gen.js";
import { UaRunTypeMapper } from "../../type-mapper.js";
import type { UaRunItemModel } from "../../types.js";

export interface UaRunListFilter {
    skip?: number;
    take?: number;
}

/**
 * Fetches runs across all automations (newest first) via the bulk `GET /runs` endpoint.
 * Results are scoped server-side to the workspaces the current user can access and each row
 * carries its automation's name, so callers need only this single request.
 */
export class UaRunListCollectionServerDataSource {
    #host: UmbControllerHost;

    constructor(host: UmbControllerHost) {
        this.#host = host;
    }

    async getCollection(filter: UaRunListFilter = {}): Promise<{
        data?: { items: UaRunItemModel[]; total: number };
        error?: unknown;
    }> {
        const { data, error } = await tryExecute(
            this.#host,
            RunsService.getRuns({
                query: {
                    skip: filter.skip ?? 0,
                    take: filter.take ?? 100,
                },
            }),
        );

        if (error || !data) {
            return { error };
        }

        const items = data.items.map(UaRunTypeMapper.toListItemModel);
        return { data: { items, total: data.total } };
    }
}
