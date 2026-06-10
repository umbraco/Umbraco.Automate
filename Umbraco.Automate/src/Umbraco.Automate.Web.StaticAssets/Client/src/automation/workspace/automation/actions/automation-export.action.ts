import { UmbWorkspaceActionBase } from "@umbraco-cms/backoffice/workspace";
import { UA_AUTOMATION_WORKSPACE_CONTEXT } from "../automation-workspace.context-token.js";
import { AutomationsService } from "../../../../api/sdk.gen.js";

export class UaAutomationExportAction extends UmbWorkspaceActionBase {
    async execute() {
        const context = await this.getContext(UA_AUTOMATION_WORKSPACE_CONTEXT);
        if (!context) return;

        const unique = context.getUnique();
        if (!unique) return;

        const { data, error } = await AutomationsService.getAutomationsByIdExport({
            path: { id: unique },
        });

        if (error || !data) return;

        const json = JSON.stringify(data, null, 2);
        const blob = new Blob([json], { type: "application/json" });
        const url = URL.createObjectURL(blob);

        const a = document.createElement("a");
        a.href = url;
        a.download = `${data.automation.alias ?? "automation"}.json`;
        a.click();

        URL.revokeObjectURL(url);
    }
}

export { UaAutomationExportAction as api };
