import { UmbEntityActionBase } from "@umbraco-cms/backoffice/entity-action";
import { UA_CREATE_WORKSPACE_MGMT_WORKSPACE_PATH_PATTERN } from "../workspace/workspace-mgmt/paths.js";

export class UaWorkspaceCreateEntityAction extends UmbEntityActionBase<never> {
    override async execute() {
        const path = UA_CREATE_WORKSPACE_MGMT_WORKSPACE_PATH_PATTERN.generateAbsolute({});
        history.pushState(null, "", path);
    }
}

export { UaWorkspaceCreateEntityAction as api };
