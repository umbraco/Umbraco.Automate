import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { UaRoutableWorkspaceContext } from "../../../core/index.js";
import { UA_AUTOMATION_GROUP_WORKSPACE_ALIAS } from "../constants.js";
import { UA_AUTOMATION_GROUP_ENTITY_TYPE } from "../../constants.js";

export class UaAutomationGroupWorkspaceContext extends UaRoutableWorkspaceContext {
    constructor(host: UmbControllerHost) {
        super(host, UA_AUTOMATION_GROUP_WORKSPACE_ALIAS, UA_AUTOMATION_GROUP_ENTITY_TYPE);
    }
}

export { UaAutomationGroupWorkspaceContext as api };
