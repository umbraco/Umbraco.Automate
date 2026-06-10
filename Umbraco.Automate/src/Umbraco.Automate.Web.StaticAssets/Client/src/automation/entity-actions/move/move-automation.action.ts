import { UA_AUTOMATION_MOVE_MODAL } from "./modal/index.js";
import { UmbEntityActionBase } from "@umbraco-cms/backoffice/entity-action";
import { UMB_MODAL_MANAGER_CONTEXT } from "@umbraco-cms/backoffice/modal";

export default class UaMoveAutomationEntityAction extends UmbEntityActionBase<never> {
    async execute() {
        const modalManager = await this.getContext(UMB_MODAL_MANAGER_CONTEXT);
        if (!modalManager) throw new Error("Modal manager not found");

        const modalContext = modalManager.open(this, UA_AUTOMATION_MOVE_MODAL, {
            data: {
                unique: this.args.unique!,
            },
        });

        await modalContext.onSubmit().catch(() => undefined);
    }
}
