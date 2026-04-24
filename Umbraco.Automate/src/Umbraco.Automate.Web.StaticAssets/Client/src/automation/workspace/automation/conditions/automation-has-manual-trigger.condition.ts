import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import type {
    UmbConditionConfigBase,
    UmbConditionControllerArguments,
    UmbExtensionCondition,
} from "@umbraco-cms/backoffice/extension-api";
import { UmbConditionBase } from "@umbraco-cms/backoffice/extension-registry";
import { UA_AUTOMATION_WORKSPACE_CONTEXT } from "../automation-workspace.context-token.js";

export const UA_AUTOMATION_HAS_MANUAL_TRIGGER_CONDITION_ALIAS =
    "Ua.Condition.Automation.HasManualTrigger";

const MANUAL_TRIGGER_ALIAS = "umbracoAutomate.manual";

/**
 * Permitted when the current automation workspace is editing an automation whose
 * trigger is the built-in Manual Trigger — used to gate the "Run now" action to
 * automations that can meaningfully be run by hand.
 */
export class UaAutomationHasManualTriggerCondition
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
                this.permitted = model?.trigger?.triggerAlias === MANUAL_TRIGGER_ALIAS;
            });
        });
    }
}

export { UaAutomationHasManualTriggerCondition as api };
