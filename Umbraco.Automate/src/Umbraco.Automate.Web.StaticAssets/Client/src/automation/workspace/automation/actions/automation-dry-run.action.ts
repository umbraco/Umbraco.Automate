import { UmbWorkspaceActionBase } from "@umbraco-cms/backoffice/workspace";
import { UA_AUTOMATION_WORKSPACE_CONTEXT } from "../automation-workspace.context-token.js";
import { client } from "../../../../api/client.gen.js";

export class UaAutomationDryRunAction extends UmbWorkspaceActionBase {
    async execute() {
        const context = await this.getContext(UA_AUTOMATION_WORKSPACE_CONTEXT);
        if (!context) return;

        const unique = context.getUnique();
        if (!unique) return;

        await client.post({
            url: `/umbraco/automate/management/api/v1/automations/${unique}/dry-run`,
            security: [{ scheme: "bearer", type: "http" }],
        });
    }
}

export { UaAutomationDryRunAction as api };
