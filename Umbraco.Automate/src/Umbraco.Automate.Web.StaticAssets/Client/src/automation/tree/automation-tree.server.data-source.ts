import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import type {
    UmbTreeDataSource,
    UmbTreeRootItemsRequestArgs,
    UmbTreeChildrenOfRequestArgs,
    UmbTreeAncestorsOfRequestArgs,
} from "@umbraco-cms/backoffice/tree";
import { UmbControllerBase } from "@umbraco-cms/backoffice/class-api";
import { tryExecute } from "@umbraco-cms/backoffice/resources";
import { AutomationsService } from "../../api/sdk.gen.js";
import type { AutomationItemResponseModel } from "../../api/types.gen.js";
import { UA_AUTOMATION_ENTITY_TYPE, UA_AUTOMATION_ROOT_ENTITY_TYPE } from "../entity.js";
import type { UaAutomationTreeItemModel } from "./types.js";

export class UaAutomationTreeServerDataSource
    extends UmbControllerBase
    implements UmbTreeDataSource<UaAutomationTreeItemModel>
{
    constructor(host: UmbControllerHost) {
        super(host);
    }

    async getRootItems(args: UmbTreeRootItemsRequestArgs) {
        const skip = args.paging && "skip" in args.paging ? args.paging.skip : 0;
        const take = args.paging && "take" in args.paging ? args.paging.take : 100;

        const { data, error } = await tryExecute(
            this,
            AutomationsService.getAutomations({
                query: { skip, take },
            }),
        );

        if (error || !data) {
            return { error };
        }

        return {
            data: {
                total: data.total,
                items: data.items.map((item) => this.#mapItem(item)),
            },
        };
    }

    async getChildrenOf(_args: UmbTreeChildrenOfRequestArgs) {
        // Flat list for now — folders will add hierarchy later.
        return {
            data: {
                total: 0,
                items: [] as UaAutomationTreeItemModel[],
            },
        };
    }

    async getAncestorsOf(_args: UmbTreeAncestorsOfRequestArgs) {
        // No ancestors for flat items.
        return { data: [] as UaAutomationTreeItemModel[] };
    }

    #mapItem(item: AutomationItemResponseModel): UaAutomationTreeItemModel {
        return {
            unique: item.id,
            entityType: UA_AUTOMATION_ENTITY_TYPE,
            parent: { unique: null, entityType: UA_AUTOMATION_ROOT_ENTITY_TYPE },
            name: item.name,
            hasChildren: false,
            isFolder: false,
            icon: "icon-mindmap",
            status: item.status,
            isEnabled: item.isEnabled,
        };
    }
}
