import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { UmbEntityActionBase, type UmbEntityActionArgs } from "@umbraco-cms/backoffice/entity-action";
import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";
import { UmbLocalizationController } from "@umbraco-cms/backoffice/localization-api";
import { AutomationsService } from "../../api/sdk.gen.js";

/**
 * Entity action that re-enables an automation auto-disabled by the circuit breaker.
 * Surfaces only when the automation's health is Disabled (see the is-disabled condition).
 */
export class UaAutomationReEnableEntityAction extends UmbEntityActionBase<never> {
    #localize = new UmbLocalizationController(this);

    constructor(host: UmbControllerHost, args: UmbEntityActionArgs<never>) {
        super(host, args);
    }

    override async execute() {
        const unique = this.args.unique;
        if (!unique) return;

        const notifications = await this.getContext(UMB_NOTIFICATION_CONTEXT);

        const { error } = await AutomationsService.postAutomationsByIdReEnable({
            path: { id: unique },
        });

        if (error) {
            const detail =
                (error as { detail?: string } | undefined)?.detail ??
                this.#localize.term("uaAutomation_reEnableFailed");

            notifications?.peek("danger", {
                data: {
                    headline: this.#localize.term("uaAutomation_reEnableFailed"),
                    message: detail,
                },
            });
            return;
        }

        notifications?.peek("positive", {
            data: {
                headline: this.#localize.term("uaAutomation_reEnableSuccess"),
                message: "",
            },
        });
    }
}

export { UaAutomationReEnableEntityAction as api };
