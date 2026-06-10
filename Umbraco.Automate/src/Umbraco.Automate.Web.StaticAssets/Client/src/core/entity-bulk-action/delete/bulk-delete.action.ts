import { UmbEntityBulkActionBase } from "@umbraco-cms/backoffice/entity-bulk-action";
import { umbConfirmModal } from "@umbraco-cms/backoffice/modal";
import { umbPeekError } from "@umbraco-cms/backoffice/notification";
import { UmbLocalizationController } from "@umbraco-cms/backoffice/localization-api";
import type { UmbDetailRepository } from "@umbraco-cms/backoffice/repository";
import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";

export interface UaBulkDeleteActionArgs {
    headline: string;
    confirmMessage: string;
    getRepository: (host: UmbControllerHost) => UmbDetailRepository<unknown>;
}

export abstract class UaBulkDeleteActionBase extends UmbEntityBulkActionBase<never> {
    #localize = new UmbLocalizationController(this);

    protected abstract getArgs(): UaBulkDeleteActionArgs;

    async execute() {
        if (!this.selection || this.selection.length === 0) {
            throw new Error("No items selected.");
        }

        const { headline, confirmMessage, getRepository } = this.getArgs();

        await umbConfirmModal(this, {
            headline,
            content: this.#localize.string(confirmMessage, this.selection.length),
            color: "danger",
            confirmLabel: "#actions_delete",
        });

        const repository = getRepository(this);

        for (const unique of this.selection) {
            const { error } = await repository.delete(unique);
            if (error) {
                const problemDetails = error as { title?: string; detail?: string };
                await umbPeekError(this, {
                    headline: problemDetails.title,
                    message: problemDetails.detail ?? problemDetails.title ?? "An item could not be deleted.",
                });
            }
        }
    }
}
