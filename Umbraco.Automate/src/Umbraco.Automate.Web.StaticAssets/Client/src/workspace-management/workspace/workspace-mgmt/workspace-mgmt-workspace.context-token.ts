import type { UaWorkspaceMgmtWorkspaceContext } from "./workspace-mgmt-workspace.context.js";
import { UmbContextToken } from "@umbraco-cms/backoffice/context-api";
import type { UmbSubmittableWorkspaceContext } from "@umbraco-cms/backoffice/workspace";
import { UA_WORKSPACE_MGMT_ENTITY_TYPE } from "../../constants.js";

export const UA_WORKSPACE_MGMT_WORKSPACE_CONTEXT = new UmbContextToken<
    UmbSubmittableWorkspaceContext,
    UaWorkspaceMgmtWorkspaceContext
>(
    "UmbWorkspaceContext",
    undefined,
    (context): context is UaWorkspaceMgmtWorkspaceContext => context.getEntityType?.() === UA_WORKSPACE_MGMT_ENTITY_TYPE,
);
