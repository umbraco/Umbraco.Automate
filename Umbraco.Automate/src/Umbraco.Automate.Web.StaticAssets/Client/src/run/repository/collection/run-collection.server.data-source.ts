import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { tryExecute } from "@umbraco-cms/backoffice/resources";
import { AutomationsService } from "../../../api/sdk.gen.js";
import { UaRunTypeMapper } from "../../type-mapper.js";
import type { UaRunItemModel } from "../../types.js";

export interface UaRunCollectionFilter {
    automationId: string;
    skip?: number;
    take?: number;
}

export class UaRunCollectionServerDataSource {
    #host: UmbControllerHost;

    constructor(host: UmbControllerHost) {
        this.#host = host;
    }

    async getCollection(filter: UaRunCollectionFilter): Promise<{
        data?: { items: UaRunItemModel[]; total: number };
        error?: unknown;
    }> {
        const { data, error } = await tryExecute(
            this.#host,
            AutomationsService.getAutomationsByIdRuns({
                path: { id: filter.automationId },
                query: {
                    skip: filter.skip ?? 0,
                    take: filter.take ?? 50,
                },
            }),
        );

        if (error || !data) {
            return { error };
        }

        const items = data.items.map(UaRunTypeMapper.toItemModel);
        return { data: { items, total: data.total } };
    }
}
