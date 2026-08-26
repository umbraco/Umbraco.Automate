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
import { UaCatalogueRepository } from "../../catalogue/repository/catalogue.repository.js";

export { UA_ENTITY_AUTOMATION_CAN_RUN_NOW_CONDITION_ALIAS } from "./automation-can-run-now.condition.constants.js";

// Per-process caches so the same automation isn't refetched every time the menu renders for the
// same entity. Tree refreshes invalidate by recreating items.
const triggerAliasCache = new Map<string, string | null>();

// Which trigger aliases can be run on demand, as reported by the catalogue. Shared across
// condition instances because the answer is a property of the installed triggers, not of any
// one automation, and each instance owns a separate repository (and so a separate cache).
let manualRunAliases: Set<string> | undefined;

/**
 * Permitted when the entity context's unique points to an automation whose configured trigger
 * can be run on demand.
 *
 * Whether a trigger can is the server's call — it implements `ISupportsManualRun` and the
 * catalogue reports `supportsManualRun` — so a provider's trigger gets a working "Run now"
 * without this condition knowing anything about it.
 *
 * Fetches the full automation from the API on first evaluation per id so the condition is
 * independent of which tree store the entity action was launched from (standalone automation
 * tree vs workspace tree).
 */
export class UaEntityAutomationCanRunNowCondition
    extends UmbConditionBase<UmbConditionConfigBase>
    implements UmbExtensionCondition
{
    #catalogue = new UaCatalogueRepository(this);

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

        const triggerAlias = await this.#resolveTriggerAlias(unique);
        if (!triggerAlias) return false;

        const runnable = await this.#resolveManualRunAliases();
        return runnable.has(triggerAlias);
    }

    async #resolveTriggerAlias(unique: string): Promise<string | null> {
        if (triggerAliasCache.has(unique)) {
            return triggerAliasCache.get(unique) ?? null;
        }

        const { data } = await tryExecute(
            this,
            AutomationsService.getAutomationsById({ path: { id: unique } }),
        );
        const triggerAlias = data?.trigger?.triggerAlias ?? null;
        triggerAliasCache.set(unique, triggerAlias);
        return triggerAlias;
    }

    async #resolveManualRunAliases(): Promise<Set<string>> {
        if (manualRunAliases) return manualRunAliases;

        // Unscoped on purpose: this asks what the installed trigger supports, which no
        // workspace's service account access can change.
        const { data } = await this.#catalogue.requestTriggers();
        if (!data) {
            // Leave it uncached so a transient failure doesn't hide "Run now" for the session.
            return new Set();
        }

        manualRunAliases = new Set(data.filter((t) => t.supportsManualRun).map((t) => t.alias));
        return manualRunAliases;
    }
}

export { UaEntityAutomationCanRunNowCondition as api };
