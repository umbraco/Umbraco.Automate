import { UmbEntityActionBase } from "@umbraco-cms/backoffice/entity-action";
import { UMB_MODAL_MANAGER_CONTEXT } from "@umbraco-cms/backoffice/modal";
import { UA_CONNECTION_TYPE_PICKER_MODAL } from "../modals/type-picker/connection-type-picker-modal.token.js";
import { UA_CREATE_CONNECTION_WORKSPACE_PATH_PATTERN } from "../workspace/connection/paths.js";

export class UaConnectionCreateEntityAction extends UmbEntityActionBase<never> {
    override async execute() {
        const modalManager = await this.getContext(UMB_MODAL_MANAGER_CONTEXT);
        if (!modalManager) return;

        const modal = modalManager.open(this, UA_CONNECTION_TYPE_PICKER_MODAL, {});

        try {
            const { typeAlias } = await modal.onSubmit();
            const path = UA_CREATE_CONNECTION_WORKSPACE_PATH_PATTERN.generateAbsolute({
                connectionType: typeAlias,
            });
            history.pushState(null, "", path);
        } catch {
            // Modal was dismissed
        }
    }
}

export { UaConnectionCreateEntityAction as api };
