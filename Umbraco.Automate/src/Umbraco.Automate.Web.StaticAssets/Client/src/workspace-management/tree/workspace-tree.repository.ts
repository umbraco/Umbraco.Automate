import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { UmbTreeRepositoryBase } from "@umbraco-cms/backoffice/tree";
import type { UmbApi } from "@umbraco-cms/backoffice/extension-api";
import { UaWorkspaceTreeServerDataSource } from "./workspace-tree.server.data-source.js";
import { UA_WORKSPACE_TREE_STORE_CONTEXT } from "./workspace-tree.store.js";
import { UA_WORKSPACE_ROOT_ENTITY_TYPE } from "../entity.js";
import type { UaWorkspaceTreeItemModel, UaWorkspaceTreeRootModel } from "./types.js";

export class UaWorkspaceTreeRepository
    extends UmbTreeRepositoryBase<UaWorkspaceTreeItemModel, UaWorkspaceTreeRootModel>
    implements UmbApi
{
    constructor(host: UmbControllerHost) {
        super(host, UaWorkspaceTreeServerDataSource, UA_WORKSPACE_TREE_STORE_CONTEXT);
    }

    async requestTreeRoot() {
        const { data: rootData } = await this._treeSource.getRootItems({ paging: { skip: 0, take: 0 } });
        const hasChildren = rootData ? rootData.total > 0 : false;

        const data: UaWorkspaceTreeRootModel = {
            unique: null,
            entityType: UA_WORKSPACE_ROOT_ENTITY_TYPE,
            name: "Automations",
            icon: "icon-layout-masonry",
            hasChildren,
            isFolder: true,
        };

        return { data };
    }
}

export default UaWorkspaceTreeRepository;
