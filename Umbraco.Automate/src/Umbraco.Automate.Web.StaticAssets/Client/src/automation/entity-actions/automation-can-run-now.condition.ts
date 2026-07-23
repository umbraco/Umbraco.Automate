import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import type {
    UmbConditionConfigBase,
    UmbConditionControllerArguments,
    UmbExtensionCondition,
} from "@umbraco-cms/backoffice/extension-api";
import { UmbConditionBase } from "@umbraco-cms/backoffice/extension-registry";
import { UMB_ENTITY_CONTEXT } from "@umbraco-cms/backoffice/entity";
import { tryExecute } from "@umbraco-cms/backoffice/resources";
import { AutomationsService } from "../../api/sdk.gen.js";
import { UA_MANUAL_TRIGGER_ALIAS, UA_SCHEDULED_TRIGGER_ALIAS } from "../constants.js";

export { UA_ENTITY_AUTOMATION_CAN_RUN_NOW_CONDITION_ALIAS } from "./automation-can-run-now.condition.constants.js";

function isManuallyRunnableTriggerAlias(alias: string | null | undefined): boolean {
    return alias === UA_MANUAL_TRIGGER_ALIAS || alias === UA_SCHEDULED_TRIGGER_ALIAS;
}

// Per-process cache so the same automation isn't refetched every time the menu
// renders for the same entity. Tree refreshes invalidate by recreating items.
const triggerAliasCache = new Map<string, string | null>();

/**
 * Permitted when the entity context's unique points to an automation whose
 * configured trigger can be run on demand (Manual or Scheduled).
 *
 * Fetches the full automation from the API on first evaluation per id so the
 * condition is independent of which tree store the entity action was launched
 * from (standalone automation tree vs workspace tree).
 */
export class UaEntityAutomationCanRunNowCondition
    extends UmbConditionBase<UmbConditionConfigBase>
    implements UmbExtensionCondition
{
    constructor(host: UmbControllerHost, args: UmbConditionControllerArguments<UmbConditionConfigBase>) {
        super(host, args);

        this.consumeContext(UMB_ENTITY_CONTEXT, (context) => {
            this.observe(context?.unique, async (unique) => {
                this.permitted = await this.#evaluate(unique ?? null);
            }, "uaEntityUnique");
        });
    }

    async #evaluate(unique: string | null): Promise<boolean> {
        if (!unique) return false;

        if (triggerAliasCache.has(unique)) {
            return isManuallyRunnableTriggerAlias(triggerAliasCache.get(unique));
        }

        const { data } = await tryExecute(
            this,
            AutomationsService.getAutomationsById({ path: { id: unique } }),
        );
        const triggerAlias = data?.trigger?.triggerAlias ?? null;
        triggerAliasCache.set(unique, triggerAlias);
        return isManuallyRunnableTriggerAlias(triggerAlias);
    }
}

export { UaEntityAutomationCanRunNowCondition as api };
