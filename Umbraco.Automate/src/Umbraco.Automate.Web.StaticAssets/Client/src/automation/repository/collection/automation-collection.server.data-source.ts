import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import type { UmbCollectionDataSource, UmbCollectionFilterModel } from "@umbraco-cms/backoffice/collection";
import { tryExecute } from "@umbraco-cms/backoffice/resources";
import { AutomationsService } from "../../../api/sdk.gen.js";
import { UaAutomationTypeMapper } from "../../type-mapper.js";
import type { UaAutomationItemModel } from "../../types.js";

export class UaAutomationCollectionServerDataSource implements UmbCollectionDataSource<UaAutomationItemModel> {
    #host: UmbControllerHost;

    constructor(host: UmbControllerHost) {
        this.#host = host;
    }

    async getCollection(filter: UmbCollectionFilterModel) {
        const { data, error } = await tryExecute(
            this.#host,
            AutomationsService.getAutomations({
                query: {
                    filter: filter.filter,
                    skip: filter.skip ?? 0,
                    take: filter.take ?? 100,
                },
            }),
        );

        if (error || !data) {
            return { error };
        }

        const items = data.items.map(UaAutomationTypeMapper.toItemModel);

        return {
            data: {
                items,
                total: data.total,
            },
        };
    }
}
