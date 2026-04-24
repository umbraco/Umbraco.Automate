import { UmbWorkspaceActionBase } from "@umbraco-cms/backoffice/workspace";
import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";
import { UmbLocalizationController } from "@umbraco-cms/backoffice/localization-api";
import { AutomationsService } from "../../../../api/sdk.gen.js";
import { UA_AUTOMATION_WORKSPACE_CONTEXT } from "../automation-workspace.context-token.js";

export class UaAutomationRunNowAction extends UmbWorkspaceActionBase {
    #localize = new UmbLocalizationController(this);

    async execute() {
        const context = await this.getContext(UA_AUTOMATION_WORKSPACE_CONTEXT);
        if (!context) return;

        const unique = context.getUnique();
        if (!unique) return;

        const notifications = await this.getContext(UMB_NOTIFICATION_CONTEXT);

        const { error } = await AutomationsService.postAutomationsByIdTrigger({
            path: { id: unique },
        });

        if (error) {
            // Most likely case: 409 when the automation isn't published+enabled.
            // Surface the server's problem detail rather than inventing our own text.
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

        notifications?.peek("positive", {
            data: {
                headline: this.#localize.term("uaAutomation_runNowStarted"),
                message: "",
            },
        });
    }
}

export { UaAutomationRunNowAction as api };
