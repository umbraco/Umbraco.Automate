import { UmbWorkspaceActionBase } from "@umbraco-cms/backoffice/workspace";
import { UA_AUTOMATION_WORKSPACE_CONTEXT } from "../automation-workspace.context-token.js";

export class UaAutomationUnpublishAction extends UmbWorkspaceActionBase {
    async execute() {
        const context = await this.getContext(UA_AUTOMATION_WORKSPACE_CONTEXT);
        await context!.unpublish();
    }
}

export { UaAutomationUnpublishAction as api };
