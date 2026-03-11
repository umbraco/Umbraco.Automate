import { UmbEntityActionBase } from "@umbraco-cms/backoffice/entity-action";
import { UA_CREATE_AUTOMATION_WORKSPACE_PATH_PATTERN } from "../workspace/automation/paths.js";

export class UaAutomationCreateEntityAction extends UmbEntityActionBase<never> {
    override async execute() {
        const path = UA_CREATE_AUTOMATION_WORKSPACE_PATH_PATTERN.generateAbsolute({});
        history.pushState(null, "", path);
    }
}

export { UaAutomationCreateEntityAction as api };
