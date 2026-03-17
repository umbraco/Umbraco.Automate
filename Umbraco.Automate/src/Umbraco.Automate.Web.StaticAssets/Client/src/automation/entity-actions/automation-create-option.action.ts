import { UmbEntityCreateOptionActionBase } from "@umbraco-cms/backoffice/entity-create-option-action";
import { UA_CREATE_AUTOMATION_WORKSPACE_PATH_PATTERN } from "../workspace/automation/paths.js";

export class UaAutomationCreateOptionAction extends UmbEntityCreateOptionActionBase {
    async getHref() {
        return UA_CREATE_AUTOMATION_WORKSPACE_PATH_PATTERN.generateAbsolute({
            parentEntityType: this.args.entityType,
            parentUnique: this.args.unique ?? "null",
        });
    }
}

export { UaAutomationCreateOptionAction as api };
