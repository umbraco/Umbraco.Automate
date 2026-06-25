import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import type { UmbSearchDataSource, UmbSearchRequestArgs } from "@umbraco-cms/backoffice/search";
import { tryExecute } from "@umbraco-cms/backoffice/resources";
import { AutomationsService } from "../../api/sdk.gen.js";
import { UA_AUTOMATION_ENTITY_TYPE } from "../constants.js";
import { UA_EDIT_AUTOMATION_WORKSPACE_PATH_PATTERN } from "../workspace/automation/paths.js";
import type { UaAutomationSearchItemModel } from "./types.js";

export class UaAutomationSearchServerDataSource implements UmbSearchDataSource<UaAutomationSearchItemModel> {
    #host: UmbControllerHost;

    constructor(host: UmbControllerHost) {
        this.#host = host;
    }

    async search(args: UmbSearchRequestArgs) {
        const { data, error } = await tryExecute(
            this.#host,
            AutomationsService.getAutomations({
                query: {
                    filter: args.query,
                    skip: args.paging?.skip ?? 0,
                    take: args.paging?.take ?? 100,
                },
            }),
        );

        if (error || !data) {
            return { error };
        }

        const items: UaAutomationSearchItemModel[] = data.items.map((item) => ({
            unique: item.id,
            name: item.name,
            entityType: UA_AUTOMATION_ENTITY_TYPE,
            href: UA_EDIT_AUTOMATION_WORKSPACE_PATH_PATTERN.generateAbsolute({ unique: item.id }),
            status: item.status,
            health: item.health,
        }));

        return { data: { items, total: data.total } };
    }
}
