import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { UaRoutableWorkspaceContext } from "../../../core/index.js";
import { UA_WORKSPACE_VIEW_WORKSPACE_ALIAS } from "../constants.js";
import { UA_WORKSPACE_ENTITY_TYPE } from "../../constants.js";

export class UaWorkspaceViewWorkspaceContext extends UaRoutableWorkspaceContext {
    constructor(host: UmbControllerHost) {
        super(host, UA_WORKSPACE_VIEW_WORKSPACE_ALIAS, UA_WORKSPACE_ENTITY_TYPE);
    }
}

export { UaWorkspaceViewWorkspaceContext as api };
