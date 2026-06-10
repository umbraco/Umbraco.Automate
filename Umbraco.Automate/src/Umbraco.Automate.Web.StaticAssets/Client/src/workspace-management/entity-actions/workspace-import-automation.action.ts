import { UmbEntityActionBase } from "@umbraco-cms/backoffice/entity-action";
import { UmbRequestReloadChildrenOfEntityEvent } from "@umbraco-cms/backoffice/entity-action";
import { UMB_ACTION_EVENT_CONTEXT } from "@umbraco-cms/backoffice/action";
import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";
import { UmbLocalizationController } from "@umbraco-cms/backoffice/localization-api";
import { UA_WORKSPACE_ENTITY_TYPE } from "../constants.js";
import { AutomationsService } from "../../api/sdk.gen.js";
import type { AutomationExportModel } from "../../api/types.gen.js";

export class UaWorkspaceImportAutomationEntityAction extends UmbEntityActionBase<never> {
    #localize = new UmbLocalizationController(this);

    override async execute() {
        const workspaceId = this.args.unique;
        if (!workspaceId) return;

        const notifications = await this.getContext(UMB_NOTIFICATION_CONTEXT);

        const file = await this.#pickFile();
        if (!file) return;

        let exportModel: AutomationExportModel;
        try {
            exportModel = JSON.parse(await file.text());
        } catch {
            notifications?.peek("danger", {
                data: {
                    headline: this.#localize.term("uaAutomation_importFailed"),
                    message: this.#localize.term("uaAutomation_importInvalidFile"),
                },
            });
            return;
        }

        const { data, error } = await AutomationsService.postAutomationsImport({
            body: { workspaceId, exportModel },
        });

        if (error || !data?.success) {
            const message = (error as { detail?: string } | undefined)?.detail
                ?? data?.errors?.join(" ")
                ?? "";
            notifications?.peek("danger", {
                data: {
                    headline: this.#localize.term("uaAutomation_importFailed"),
                    message,
                },
            });
            return;
        }

        notifications?.peek("positive", {
            data: {
                headline: this.#localize.term("uaAutomation_importSuccess"),
                message: data.automationAlias ?? "",
            },
        });

        if (data.warnings.length > 0) {
            notifications?.peek("warning", {
                data: {
                    headline: this.#localize.term("uaAutomation_importWarnings"),
                    message: data.warnings.join("\n"),
                },
            });
        }

        const eventContext = await this.getContext(UMB_ACTION_EVENT_CONTEXT);
        eventContext?.dispatchEvent(
            new UmbRequestReloadChildrenOfEntityEvent({
                entityType: UA_WORKSPACE_ENTITY_TYPE,
                unique: workspaceId,
            }),
        );
    }

    #pickFile(): Promise<File | null> {
        return new Promise((resolve) => {
            const input = document.createElement("input");
            input.type = "file";
            input.accept = ".json,application/json";
            input.addEventListener("change", () => resolve(input.files?.[0] ?? null));
            input.addEventListener("cancel", () => resolve(null));
            input.click();
        });
    }
}

export { UaWorkspaceImportAutomationEntityAction as api };
