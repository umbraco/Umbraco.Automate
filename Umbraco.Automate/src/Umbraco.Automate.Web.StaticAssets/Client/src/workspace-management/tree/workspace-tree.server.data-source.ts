import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import type {
    UmbTreeDataSource,
    UmbTreeRootItemsRequestArgs,
    UmbTreeChildrenOfRequestArgs,
    UmbTreeAncestorsOfRequestArgs,
} from "@umbraco-cms/backoffice/tree";
import { UmbControllerBase } from "@umbraco-cms/backoffice/class-api";
import { tryExecute } from "@umbraco-cms/backoffice/resources";
import { WorkspacesService } from "../../api/sdk.gen.js";
import type { WorkspaceItemResponseModel } from "../../api/types.gen.js";
import { UA_WORKSPACE_ENTITY_TYPE, UA_WORKSPACE_ROOT_ENTITY_TYPE } from "../entity.js";
import type { UaWorkspaceTreeItemModel } from "./types.js";

export class UaWorkspaceTreeServerDataSource
    extends UmbControllerBase
    implements UmbTreeDataSource<UaWorkspaceTreeItemModel>
{
    constructor(host: UmbControllerHost) {
        super(host);
    }

    async getRootItems(args: UmbTreeRootItemsRequestArgs) {
        const skip = args.paging && "skip" in args.paging ? args.paging.skip : 0;
        const take = args.paging && "take" in args.paging ? args.paging.take : 100;

        const { data, error } = await tryExecute(
            this,
            WorkspacesService.getWorkspaces({
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
                items: [] as UaWorkspaceTreeItemModel[],
            },
        };
    }

    async getAncestorsOf(_args: UmbTreeAncestorsOfRequestArgs) {
        // No ancestors for flat items.
        return { data: [] as UaWorkspaceTreeItemModel[] };
    }

    #mapItem(item: WorkspaceItemResponseModel): UaWorkspaceTreeItemModel {
        return {
            unique: item.id,
            entityType: UA_WORKSPACE_ENTITY_TYPE,
            parent: { unique: null, entityType: UA_WORKSPACE_ROOT_ENTITY_TYPE },
            name: item.name,
            hasChildren: false,
            isFolder: false,
            icon: "icon-users",
        };
    }
}
