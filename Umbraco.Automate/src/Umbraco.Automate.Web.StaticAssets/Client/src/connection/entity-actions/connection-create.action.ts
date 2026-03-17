import { UmbEntityActionBase } from "@umbraco-cms/backoffice/entity-action";
import { UA_CREATE_CONNECTION_WORKSPACE_PATH_PATTERN } from "../workspace/connection/paths.js";

export class UaConnectionCreateEntityAction extends UmbEntityActionBase<never> {
    override async execute() {
        const path = UA_CREATE_CONNECTION_WORKSPACE_PATH_PATTERN.generateAbsolute({});
        history.pushState(null, "", path);
    }
}

export { UaConnectionCreateEntityAction as api };
