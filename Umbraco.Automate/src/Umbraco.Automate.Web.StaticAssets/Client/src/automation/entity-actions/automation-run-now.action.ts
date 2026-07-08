import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { UmbEntityActionBase, type UmbEntityActionArgs } from "@umbraco-cms/backoffice/entity-action";
import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";
import { UmbLocalizationController } from "@umbraco-cms/backoffice/localization-api";
import { AutomationsService } from "../../api/sdk.gen.js";
import { UaAutomationRunsChangedEvent } from "../events/automation-runs-changed.event.js";
import { dispatchActionEvent } from "../../core/utils/event.utils.js";

/**
 * Entity action that triggers a manual-trigger automation run.
 * Mirrors the in-workspace "Run now" action so users can fire automations from the
 * tree/list without opening the editor first.
 */
export class UaAutomationRunNowEntityAction extends UmbEntityActionBase<never> {
    #localize = new UmbLocalizationController(this);

    constructor(host: UmbControllerHost, args: UmbEntityActionArgs<never>) {
        super(host, args);
    }

    override async execute() {
        const unique = this.args.unique;
        if (!unique) return;

        const notifications = await this.getContext(UMB_NOTIFICATION_CONTEXT);

        const { error } = await AutomationsService.postAutomationsByIdTrigger({
            path: { id: unique },
        });

        if (error) {
            // Most likely 409 when the automation isn't published+enabled — surface the
            // server's problem detail rather than inventing our own copy.
            const detail =
                (error as { detail?: string } | undefined)?.detail ??
                this.#localize.term("uaAutomation_runNowFailed");

            notifications?.peek("danger", {
                data: {
                    headline: this.#localize.term("uaAutomation_runNowFailed"),
                    message: detail,
                },
            });
            return;
        }

        // Let the runs view refresh now the run has been triggered.
        dispatchActionEvent(this, new UaAutomationRunsChangedEvent(unique));

        notifications?.peek("positive", {
            data: {
                headline: this.#localize.term("uaAutomation_runNowStarted"),
                message: "",
            },
        });
    }
}

export { UaAutomationRunNowEntityAction as api };
