import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { UmbTreeRepositoryBase } from "@umbraco-cms/backoffice/tree";
import type { UmbApi } from "@umbraco-cms/backoffice/extension-api";
import { UaWorkspaceMgmtTreeServerDataSource } from "./workspace-mgmt-tree.server.data-source.js";
import { UA_WORKSPACE_MGMT_ROOT_ENTITY_TYPE } from "../entity.js";
import type { UaWorkspaceMgmtTreeItemModel, UaWorkspaceMgmtTreeRootModel } from "./types.js";

export class UaWorkspaceMgmtTreeRepository
    extends UmbTreeRepositoryBase<UaWorkspaceMgmtTreeItemModel, UaWorkspaceMgmtTreeRootModel>
    implements UmbApi
{
    constructor(host: UmbControllerHost) {
        super(host, UaWorkspaceMgmtTreeServerDataSource);
    }

    async requestTreeRoot() {
        const { data: rootData } = await this._treeSource.getRootItems({ paging: { skip: 0, take: 0 } });
        const hasChildren = rootData ? rootData.total > 0 : false;

        const data: UaWorkspaceMgmtTreeRootModel = {
            unique: null,
            entityType: UA_WORKSPACE_MGMT_ROOT_ENTITY_TYPE,
            name: "Workspaces",
            icon: "icon-layout-masonry",
            hasChildren,
            isFolder: true,
        };

        return { data };
    }
}

export default UaWorkspaceMgmtTreeRepository;
