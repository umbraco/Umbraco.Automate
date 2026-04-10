import { UmbWorkspaceActionBase } from "@umbraco-cms/backoffice/workspace";
import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";
import { UA_AUTOMATION_WORKSPACE_CONTEXT } from "../automation-workspace.context-token.js";
import { client } from "../../../../api/client.gen.js";

export class UaAutomationDryRunAction extends UmbWorkspaceActionBase {
    async execute() {
        const context = await this.getContext(UA_AUTOMATION_WORKSPACE_CONTEXT);
        if (!context) return;

        const unique = context.getUnique();
        if (!unique) return;

        const notificationContext = await this.getContext(UMB_NOTIFICATION_CONTEXT);

        try {
            const { response } = await client.post({
                url: `/umbraco/automate/management/api/v1/automations/${unique}/dry-run`,
                security: [{ scheme: "bearer", type: "http" }],
            });

            if (response.ok) {
                notificationContext?.peek("positive", {
                    data: { headline: "Dry run started", message: "Check the Runs tab for results." },
                });
            } else if (response.status === 409) {
                notificationContext?.peek("warning", {
                    data: { headline: "Cannot dry run", message: "The automation must be published and enabled." },
                });
            } else {
                notificationContext?.peek("danger", {
                    data: { headline: "Dry run failed", message: `Server returned ${response.status}.` },
                });
            }
        } catch {
            notificationContext?.peek("danger", {
                data: { headline: "Dry run failed", message: "An unexpected error occurred." },
            });
        }
    }
}

export { UaAutomationDryRunAction as api };
