import { UA_WORKSPACE_TREE_REPOSITORY_ALIAS } from "../../workspace-management/tree/constants.js";
import { UmbMenuTreeStructureWorkspaceContextBase } from "@umbraco-cms/backoffice/menu";
import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";

export class UaAutomationMenuStructureContext extends UmbMenuTreeStructureWorkspaceContextBase {
    constructor(host: UmbControllerHost) {
        super(host, { treeRepositoryAlias: UA_WORKSPACE_TREE_REPOSITORY_ALIAS });
    }
}

export default UaAutomationMenuStructureContext;
