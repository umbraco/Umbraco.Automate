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

export const UA_ENTITY_AUTOMATION_HAS_MANUAL_TRIGGER_CONDITION_ALIAS =
    "Ua.Condition.Entity.Automation.HasManualTrigger";

const MANUAL_TRIGGER_ALIAS = "umbracoAutomate.manual";

// Per-process cache so the same automation isn't refetched every time the menu
// renders for the same entity. Tree refreshes invalidate by recreating items.
const triggerAliasCache = new Map<string, string | null>();

/**
 * Permitted when the entity context's unique points to an automation whose
 * configured trigger is the Manual trigger.
 *
 * Fetches the full automation from the API on first evaluation per id so the
 * condition is independent of which tree store the entity action was launched
 * from (standalone automation tree vs workspace tree).
 */
export class UaEntityAutomationHasManualTriggerCondition
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
            return triggerAliasCache.get(unique) === MANUAL_TRIGGER_ALIAS;
        }

        const { data } = await tryExecute(
            this,
            AutomationsService.getAutomationsById({ path: { id: unique } }),
        );
        const triggerAlias = data?.trigger?.triggerAlias ?? null;
        triggerAliasCache.set(unique, triggerAlias);
        return triggerAlias === MANUAL_TRIGGER_ALIAS;
    }
}

export { UaEntityAutomationHasManualTriggerCondition as api };
