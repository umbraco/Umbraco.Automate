import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { UmbTreeRepositoryBase } from "@umbraco-cms/backoffice/tree";
import type { UmbApi } from "@umbraco-cms/backoffice/extension-api";
import { UaConnectionTreeServerDataSource } from "./connection-tree.server.data-source.js";
import { UA_CONNECTION_ROOT_ENTITY_TYPE } from "../entity.js";
import type { UaConnectionTreeItemModel, UaConnectionTreeRootModel } from "./types.js";

export class UaConnectionTreeRepository
    extends UmbTreeRepositoryBase<UaConnectionTreeItemModel, UaConnectionTreeRootModel>
    implements UmbApi
{
    constructor(host: UmbControllerHost) {
        super(host, UaConnectionTreeServerDataSource);
    }

    async requestTreeRoot() {
        const { data: rootData } = await this._treeSource.getRootItems({ paging: { skip: 0, take: 0 } });
        const hasChildren = rootData ? rootData.total > 0 : false;

        const data: UaConnectionTreeRootModel = {
            unique: null,
            entityType: UA_CONNECTION_ROOT_ENTITY_TYPE,
            name: "Connections",
            icon: "icon-usb-connector",
            hasChildren,
            isFolder: true,
        };

        return { data };
    }
}

export default UaConnectionTreeRepository;
