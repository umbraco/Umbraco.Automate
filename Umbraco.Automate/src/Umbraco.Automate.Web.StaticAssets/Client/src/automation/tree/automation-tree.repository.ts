import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { UmbTreeRepositoryBase } from "@umbraco-cms/backoffice/tree";
import type { UmbApi } from "@umbraco-cms/backoffice/extension-api";
import { UaAutomationTreeServerDataSource } from "./automation-tree.server.data-source.js";
import { UA_AUTOMATION_TREE_STORE_CONTEXT } from "./automation-tree.store.js";
import { UA_AUTOMATION_ROOT_ENTITY_TYPE } from "../entity.js";
import type { UaAutomationTreeItemModel, UaAutomationTreeRootModel } from "./types.js";

export class UaAutomationTreeRepository
    extends UmbTreeRepositoryBase<UaAutomationTreeItemModel, UaAutomationTreeRootModel>
    implements UmbApi
{
    constructor(host: UmbControllerHost) {
        super(host, UaAutomationTreeServerDataSource, UA_AUTOMATION_TREE_STORE_CONTEXT);
    }

    async requestTreeRoot() {
        const { data: rootData } = await this._treeSource.getRootItems({ paging: { skip: 0, take: 0 } });
        const hasChildren = rootData ? rootData.total > 0 : false;

        const data: UaAutomationTreeRootModel = {
            unique: null,
            entityType: UA_AUTOMATION_ROOT_ENTITY_TYPE,
            name: "Automations",
            hasChildren,
            isFolder: true,
        };

        return { data };
    }
}

export default UaAutomationTreeRepository;
