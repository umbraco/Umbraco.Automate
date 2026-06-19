import type { UmbSearchProvider, UmbSearchRequestArgs } from "@umbraco-cms/backoffice/search";
import { UmbControllerBase } from "@umbraco-cms/backoffice/class-api";
import { UaAutomationSearchRepository } from "./automation-search.repository.js";
import type { UaAutomationSearchItemModel } from "./types.js";

export class UaAutomationSearchProvider
    extends UmbControllerBase
    implements UmbSearchProvider<UaAutomationSearchItemModel>
{
    #repository = new UaAutomationSearchRepository(this);

    search(args: UmbSearchRequestArgs) {
        return this.#repository.search(args);
    }
}

export { UaAutomationSearchProvider as api };
