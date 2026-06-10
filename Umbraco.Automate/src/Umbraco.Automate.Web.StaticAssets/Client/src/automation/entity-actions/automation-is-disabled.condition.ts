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

export { UA_ENTITY_AUTOMATION_IS_DISABLED_CONDITION_ALIAS } from "./automation-is-disabled.condition.constants.js";

/**
 * Permitted when the entity context's unique points to an automation whose circuit-breaker
 * health is Disabled. Fetches the automation fresh on each evaluation (no cache) because health
 * changes when the automation is re-enabled.
 */
export class UaEntityAutomationIsDisabledCondition
    extends UmbConditionBase<UmbConditionConfigBase>
    implements UmbExtensionCondition
{
    constructor(host: UmbControllerHost, args: UmbConditionControllerArguments<UmbConditionConfigBase>) {
        super(host, args);

        this.consumeContext(UMB_ENTITY_CONTEXT, (context) => {
            this.observe(
                context?.unique,
                async (unique) => {
                    this.permitted = await this.#evaluate(unique ?? null);
                },
                "uaEntityUnique",
            );
        });
    }

    async #evaluate(unique: string | null): Promise<boolean> {
        if (!unique) return false;

        const { data } = await tryExecute(
            this,
            AutomationsService.getAutomationsById({ path: { id: unique } }),
        );

        return data?.health === "Disabled";
    }
}

export { UaEntityAutomationIsDisabledCondition as api };
