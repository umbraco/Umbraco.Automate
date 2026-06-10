import { UmbWorkspaceActionBase } from "@umbraco-cms/backoffice/workspace";
import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";
import { UmbLocalizationController } from "@umbraco-cms/backoffice/localization-api";
import { UA_AUTOMATION_WORKSPACE_CONTEXT } from "../automation-workspace.context-token.js";

export class UaAutomationUnpublishAction extends UmbWorkspaceActionBase {
    #localize = new UmbLocalizationController(this);

    async execute() {
        const context = await this.getContext(UA_AUTOMATION_WORKSPACE_CONTEXT);
        if (!context) return;

        const notifications = await this.getContext(UMB_NOTIFICATION_CONTEXT);

        try {
            await context.unpublish();
        } catch (error) {
            const detail =
                (error as { detail?: string } | undefined)?.detail ??
                this.#localize.term("uaAutomation_unpublishFailed");

            notifications?.peek("danger", {
                data: {
                    headline: this.#localize.term("uaAutomation_unpublishFailed"),
                    message: detail,
                },
            });
            return;
        }

        notifications?.peek("positive", {
            data: {
                headline: this.#localize.term("uaAutomation_unpublishSuccess"),
                message: "",
            },
        });
    }
}

export { UaAutomationUnpublishAction as api };
