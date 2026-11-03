import { UmbWorkspaceActionBase } from "@umbraco-cms/backoffice/workspace";
import { UA_AUTOMATION_WORKSPACE_CONTEXT } from "../automation-workspace.context-token.js";

export class UaAutomationSaveAndPublishAction extends UmbWorkspaceActionBase {
    async execute() {
        const context = await this.getContext(UA_AUTOMATION_WORKSPACE_CONTEXT);
        if (!context) throw new Error("Workspace context not available");
        // requestSubmit() runs the editor's validation (e.g. the required name) first, the same as
        // plain Save, and rejects when it fails, so an invalid automation is never sent or published.
        await context.requestSubmit();
        await context.publish();
    }
}

export { UaAutomationSaveAndPublishAction as api };
