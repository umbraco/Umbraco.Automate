import { UaBulkDeleteActionBase, type UaBulkDeleteActionArgs } from "../../../core/index.js";
import { UaWorkspaceDetailRepository } from "../../repository/detail/workspace-detail.repository.js";

export class UaWorkspaceBulkDeleteAction extends UaBulkDeleteActionBase {
    protected getArgs(): UaBulkDeleteActionArgs {
        return {
            headline: "#actions_delete",
            confirmMessage: "#uaWorkspace_bulkDeleteConfirm",
            getRepository: (host) => new UaWorkspaceDetailRepository(host),
        };
    }
}

export { UaWorkspaceBulkDeleteAction as api };
