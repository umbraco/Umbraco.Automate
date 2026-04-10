import { UmbEntityActionBase } from "@umbraco-cms/backoffice/entity-action";
import { AutomationsService } from "../../api/sdk.gen.js";

export class UaAutomationExportAction extends UmbEntityActionBase<never> {
    override async execute() {
        const unique = this.args.unique;
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
