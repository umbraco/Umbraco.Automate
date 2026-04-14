import { UmbWorkspaceActionBase } from "@umbraco-cms/backoffice/workspace";
import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";
import { UA_AUTOMATION_WORKSPACE_CONTEXT } from "../automation-workspace.context-token.js";
import { client } from "../../../../api/client.gen.js";
import { RunsService } from "../../../../api/sdk.gen.js";
import type { AutomationRunResponseModel } from "../../../../api/types.gen.js";

const TERMINAL_STATUSES = ["Completed", "Failed", "Cancelled"];
const POLL_INTERVAL_MS = 500;
const MAX_POLLS = 15;

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

            if (response.status === 409) {
                notificationContext?.peek("warning", {
                    data: { headline: "Cannot dry run", message: "The automation must be published and enabled." },
                });
                return;
            }

            if (!response.ok) {
                notificationContext?.peek("danger", {
                    data: { headline: "Dry run failed", message: `Server returned ${response.status}.` },
                });
                return;
            }

            const body = await response.json();
            const runId: string | undefined = body?.runId;

            if (!runId) {
                notificationContext?.peek("positive", {
                    data: { headline: "Dry run started", message: "Check the Runs tab for results." },
                });
                return;
            }

            // Poll until the run reaches a terminal state.
            const run = await this.#pollForCompletion(runId);

            if (run) {
                context.showDryRunOverlay(run);
            } else {
                notificationContext?.peek("positive", {
                    data: { headline: "Dry run started", message: "Check the Runs tab for results." },
                });
            }
        } catch {
            notificationContext?.peek("danger", {
                data: { headline: "Dry run failed", message: "An unexpected error occurred." },
            });
        }
    }

    async #pollForCompletion(runId: string): Promise<AutomationRunResponseModel | null> {
        for (let i = 0; i < MAX_POLLS; i++) {
            const { data } = await RunsService.getRunsById({ path: { id: runId } });
            if (data && TERMINAL_STATUSES.includes(data.status)) {
                return data as AutomationRunResponseModel;
            }
            await new Promise((r) => setTimeout(r, POLL_INTERVAL_MS));
        }
        return null;
    }
}

export { UaAutomationDryRunAction as api };
