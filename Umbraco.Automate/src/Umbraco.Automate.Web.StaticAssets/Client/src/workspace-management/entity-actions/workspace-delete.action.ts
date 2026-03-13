import { UaDeleteActionBase, type UaDeleteActionArgs } from "../../core/index.js";
import { UaWorkspaceDetailRepository } from "../repository/detail/workspace-detail.repository.js";

export class UaWorkspaceDeleteAction extends UaDeleteActionBase {
    protected getArgs(): UaDeleteActionArgs {
        return {
            headline: "#actions_delete",
            confirmMessage: "#uaWorkspace_deleteConfirm",
            getRepository: (host) => new UaWorkspaceDetailRepository(host),
        };
    }
}

export { UaWorkspaceDeleteAction as api };
