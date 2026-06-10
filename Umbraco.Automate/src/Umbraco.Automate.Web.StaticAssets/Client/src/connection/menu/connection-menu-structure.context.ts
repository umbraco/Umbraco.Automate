import { UA_CONNECTION_TREE_REPOSITORY_ALIAS } from "../tree/constants.js";
import { UmbMenuTreeStructureWorkspaceContextBase } from "@umbraco-cms/backoffice/menu";
import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";

export class UaConnectionMenuStructureContext extends UmbMenuTreeStructureWorkspaceContextBase {
    constructor(host: UmbControllerHost) {
        super(host, { treeRepositoryAlias: UA_CONNECTION_TREE_REPOSITORY_ALIAS });
    }
}

export default UaConnectionMenuStructureContext;
