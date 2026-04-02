import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import type { UmbConditionConfigBase, UmbConditionControllerArguments, UmbExtensionCondition } from "@umbraco-cms/backoffice/extension-api";
import { UmbConditionBase } from "@umbraco-cms/backoffice/extension-registry";
import { UMB_ACTION_EVENT_CONTEXT } from "@umbraco-cms/backoffice/action";
import { UaWorkspaceCollectionServerDataSource } from "../../workspace-management/repository/collection/workspace-collection.server.data-source.js";
import { UaEntityActionEvent } from "../../core/index.js";
import { UA_WORKSPACE_ENTITY_TYPE } from "../../workspace-management/constants.js";

export const UA_WORKSPACES_EXIST_CONDITION_ALIAS = "Ua.Condition.WorkspacesExist";

export class UaWorkspacesExistCondition
    extends UmbConditionBase<UmbConditionConfigBase>
    implements UmbExtensionCondition
{
    constructor(host: UmbControllerHost, args: UmbConditionControllerArguments<UmbConditionConfigBase>) {
        super(host, args);
        this.#check();

        this.consumeContext(UMB_ACTION_EVENT_CONTEXT, (context) => {
            if (!context) return;
            context.addEventListener(UaEntityActionEvent.CREATED, (event) => {
                if ((event as UaEntityActionEvent).getEntityType() === UA_WORKSPACE_ENTITY_TYPE) {
                    this.#check();
                }
            });
            context.addEventListener(UaEntityActionEvent.DELETED, (event) => {
                if ((event as UaEntityActionEvent).getEntityType() === UA_WORKSPACE_ENTITY_TYPE) {
                    this.#check();
                }
            });
        });
    }

    async #check() {
        const source = new UaWorkspaceCollectionServerDataSource(this._host);
        const { data } = await source.getCollection({ skip: 0, take: 1 });
        this.permitted = (data?.total ?? 0) > 0;
    }
}

export { UaWorkspacesExistCondition as api };
