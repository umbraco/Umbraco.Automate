import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import type { UmbSearchRepository, UmbSearchRequestArgs } from "@umbraco-cms/backoffice/search";
import { UmbControllerBase } from "@umbraco-cms/backoffice/class-api";
import { UaAutomationSearchServerDataSource } from "./automation-search.server.data-source.js";
import type { UaAutomationSearchItemModel } from "./types.js";

export class UaAutomationSearchRepository
    extends UmbControllerBase
    implements UmbSearchRepository<UaAutomationSearchItemModel>
{
    #dataSource: UaAutomationSearchServerDataSource;

    constructor(host: UmbControllerHost) {
        super(host);
        this.#dataSource = new UaAutomationSearchServerDataSource(this);
    }

    search(args: UmbSearchRequestArgs) {
        return this.#dataSource.search(args);
    }
}
