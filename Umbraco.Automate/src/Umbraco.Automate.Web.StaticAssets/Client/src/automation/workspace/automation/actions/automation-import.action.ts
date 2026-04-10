import { UmbWorkspaceActionBase } from "@umbraco-cms/backoffice/workspace";
import { UA_AUTOMATION_WORKSPACE_CONTEXT } from "../automation-workspace.context-token.js";
import { AutomationsService } from "../../../../api/sdk.gen.js";

export class UaAutomationImportAction extends UmbWorkspaceActionBase {
    async execute() {
        const context = await this.getContext(UA_AUTOMATION_WORKSPACE_CONTEXT);
        if (!context) return;

        const data = context.getData();
        const workspaceId = data?.workspaceId;
        if (!workspaceId) return;

        // Open file picker
        const file = await this.#pickFile();
        if (!file) return;

        const text = await file.text();
        let exportModel: unknown;
        try {
            exportModel = JSON.parse(text);
        } catch {
            return;
        }

        await AutomationsService.postAutomationsImport({
            body: {
                workspaceId,
                exportModel: exportModel as never,
            },
        });
    }

    #pickFile(): Promise<File | null> {
        return new Promise((resolve) => {
            const input = document.createElement("input");
            input.type = "file";
            input.accept = ".json";
            input.addEventListener("change", () => {
                resolve(input.files?.[0] ?? null);
            });
            input.addEventListener("cancel", () => resolve(null));
            input.click();
        });
    }
}

export { UaAutomationImportAction as api };
