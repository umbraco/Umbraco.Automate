import { UaBulkDeleteActionBase, type UaBulkDeleteActionArgs } from "../../../core/index.js";
import { UaAutomationDetailRepository } from "../../repository/detail/automation-detail.repository.js";

export class UaAutomationBulkDeleteAction extends UaBulkDeleteActionBase {
    protected getArgs(): UaBulkDeleteActionArgs {
        return {
            headline: "#actions_delete",
            confirmMessage: "#uaAutomation_bulkDeleteConfirm",
            getRepository: (host) => new UaAutomationDetailRepository(host),
        };
    }
}

export { UaAutomationBulkDeleteAction as api };
