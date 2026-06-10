import { UmbWorkspaceActionBase } from "@umbraco-cms/backoffice/workspace";
import { UA_AUTOMATION_WORKSPACE_CONTEXT } from "../automation-workspace.context-token.js";

export class UaAutomationSaveAndPublishAction extends UmbWorkspaceActionBase {
    async execute() {
        const context = await this.getContext(UA_AUTOMATION_WORKSPACE_CONTEXT);
        if (!context) throw new Error("Workspace context not available");
        await context.submit();
        await context.publish();
    }
}

export { UaAutomationSaveAndPublishAction as api };
