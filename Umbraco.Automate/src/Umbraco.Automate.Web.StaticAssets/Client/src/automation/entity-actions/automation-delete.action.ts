import { UaDeleteActionBase, type UaDeleteActionArgs } from "../../core/index.js";
import { UaAutomationDetailRepository } from "../repository/detail/automation-detail.repository.js";

export class UaAutomationDeleteAction extends UaDeleteActionBase {
    protected getArgs(): UaDeleteActionArgs {
        return {
            headline: "#actions_delete",
            confirmMessage: "#uaAutomation_deleteConfirm",
            getRepository: (host) => new UaAutomationDetailRepository(host),
        };
    }
}

export { UaAutomationDeleteAction as api };
