import { UaBulkDeleteActionBase, type UaBulkDeleteActionArgs } from "../../../core/index.js";
import { UaConnectionDetailRepository } from "../../repository/detail/connection-detail.repository.js";

export class UaConnectionBulkDeleteAction extends UaBulkDeleteActionBase {
    protected getArgs(): UaBulkDeleteActionArgs {
        return {
            headline: "#actions_delete",
            confirmMessage: "#uaConnection_bulkDeleteConfirm",
            getRepository: (host) => new UaConnectionDetailRepository(host),
        };
    }
}

export { UaConnectionBulkDeleteAction as api };
