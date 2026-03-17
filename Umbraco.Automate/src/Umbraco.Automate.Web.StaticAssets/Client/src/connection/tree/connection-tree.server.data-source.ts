import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import type {
    UmbTreeDataSource,
    UmbTreeRootItemsRequestArgs,
    UmbTreeChildrenOfRequestArgs,
    UmbTreeAncestorsOfRequestArgs,
} from "@umbraco-cms/backoffice/tree";
import { UmbControllerBase } from "@umbraco-cms/backoffice/class-api";
import { tryExecute } from "@umbraco-cms/backoffice/resources";
import { ConnectionsService } from "../../api/sdk.gen.js";
import type { ConnectionItemResponseModel } from "../../api/types.gen.js";
import { UA_CONNECTION_ENTITY_TYPE, UA_CONNECTION_ROOT_ENTITY_TYPE } from "../entity.js";
import type { UaConnectionTreeItemModel } from "./types.js";

export class UaConnectionTreeServerDataSource
    extends UmbControllerBase
    implements UmbTreeDataSource<UaConnectionTreeItemModel>
{
    constructor(host: UmbControllerHost) {
        super(host);
    }

    async getRootItems(args: UmbTreeRootItemsRequestArgs) {
        const skip = args.paging && "skip" in args.paging ? args.paging.skip : 0;
        const take = args.paging && "take" in args.paging ? args.paging.take : 100;

        const { data, error } = await tryExecute(
            this,
            ConnectionsService.getConnections({
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
        // Flat list — no hierarchy.
        return {
            data: {
                total: 0,
                items: [] as UaConnectionTreeItemModel[],
            },
        };
    }

    async getAncestorsOf(_args: UmbTreeAncestorsOfRequestArgs) {
        // No ancestors for flat items.
        return { data: [] as UaConnectionTreeItemModel[] };
    }

    #mapItem(item: ConnectionItemResponseModel): UaConnectionTreeItemModel {
        return {
            unique: item.id,
            entityType: UA_CONNECTION_ENTITY_TYPE,
            parent: { unique: null, entityType: UA_CONNECTION_ROOT_ENTITY_TYPE },
            name: item.name,
            hasChildren: false,
            isFolder: false,
            icon: "icon-link",
        };
    }
}
