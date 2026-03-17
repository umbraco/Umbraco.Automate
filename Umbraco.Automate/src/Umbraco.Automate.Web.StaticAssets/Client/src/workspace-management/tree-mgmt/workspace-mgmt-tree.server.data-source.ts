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
import { UA_WORKSPACE_MGMT_ENTITY_TYPE, UA_WORKSPACE_MGMT_ROOT_ENTITY_TYPE } from "../entity.js";
import type { UaWorkspaceMgmtTreeItemModel } from "./types.js";

export class UaWorkspaceMgmtTreeServerDataSource
    extends UmbControllerBase
    implements UmbTreeDataSource<UaWorkspaceMgmtTreeItemModel>
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
        // Flat list — management tree has no hierarchy.
        return {
            data: {
                total: 0,
                items: [] as UaWorkspaceMgmtTreeItemModel[],
            },
        };
    }

    async getAncestorsOf(_args: UmbTreeAncestorsOfRequestArgs) {
        return { data: [] as UaWorkspaceMgmtTreeItemModel[] };
    }

    #mapItem(item: WorkspaceItemResponseModel): UaWorkspaceMgmtTreeItemModel {
        return {
            unique: item.id,
            entityType: UA_WORKSPACE_MGMT_ENTITY_TYPE,
            parent: { unique: null, entityType: UA_WORKSPACE_MGMT_ROOT_ENTITY_TYPE },
            name: item.name,
            hasChildren: false,
            isFolder: false,
            icon: "icon-users",
        };
    }
}
