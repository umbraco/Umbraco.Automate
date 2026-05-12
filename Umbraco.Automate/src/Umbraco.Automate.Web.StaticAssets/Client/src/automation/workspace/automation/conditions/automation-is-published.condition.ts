import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import type {
    UmbConditionConfigBase,
    UmbConditionControllerArguments,
    UmbExtensionCondition,
} from "@umbraco-cms/backoffice/extension-api";
import { UmbConditionBase } from "@umbraco-cms/backoffice/extension-registry";
import { UA_AUTOMATION_WORKSPACE_CONTEXT } from "../automation-workspace.context-token.js";

export { UA_AUTOMATION_IS_PUBLISHED_CONDITION_ALIAS } from "./automation-is-published.condition.constants.js";

export class UaAutomationIsPublishedCondition
    extends UmbConditionBase<UmbConditionConfigBase>
    implements UmbExtensionCondition
{
    constructor(host: UmbControllerHost, args: UmbConditionControllerArguments<UmbConditionConfigBase>) {
        super(host, args);

        this.consumeContext(UA_AUTOMATION_WORKSPACE_CONTEXT, (context) => {
            if (!context) {
                this.permitted = false;
                return;
            }

            this.observe(context.data, (model) => {
                this.permitted = model?.status === "Published";
            });
        });
    }
}

export { UaAutomationIsPublishedCondition as api };
