import { UmbEntityActionBase } from "@umbraco-cms/backoffice/entity-action";
import { umbConfirmModal } from "@umbraco-cms/backoffice/modal";
import { umbPeekError } from "@umbraco-cms/backoffice/notification";
import type { UmbDetailRepository } from "@umbraco-cms/backoffice/repository";
import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";

export interface UaDeleteActionArgs {
    headline: string;
    confirmMessage: string;
    getRepository: (host: UmbControllerHost) => UmbDetailRepository<unknown>;
}

export abstract class UaDeleteActionBase extends UmbEntityActionBase<never> {
    protected abstract getArgs(): UaDeleteActionArgs;

    async execute() {
        if (!this.args.unique) {
            throw new Error("Cannot delete without unique identifier.");
        }

        const { headline, confirmMessage, getRepository } = this.getArgs();

        await umbConfirmModal(this, {
            headline,
            content: confirmMessage,
            color: "danger",
            confirmLabel: "#actions_delete",
        });

        const repository = getRepository(this);
        const { error } = await repository.delete(this.args.unique);

        if (error) {
            const problemDetails = error as { title?: string; detail?: string };
            await umbPeekError(this, {
                headline: problemDetails.title,
                message: problemDetails.detail ?? problemDetails.title ?? "The item could not be deleted.",
            });
            throw error;
        }
    }
}
