import type { UaConnectionWorkspaceContext } from "./connection-workspace.context.js";
import { UmbContextToken } from "@umbraco-cms/backoffice/context-api";
import type { UmbSubmittableWorkspaceContext } from "@umbraco-cms/backoffice/workspace";
import { UA_CONNECTION_ENTITY_TYPE } from "../../constants.js";

export const UA_CONNECTION_WORKSPACE_CONTEXT = new UmbContextToken<
    UmbSubmittableWorkspaceContext,
    UaConnectionWorkspaceContext
>(
    "UmbWorkspaceContext",
    undefined,
    (context): context is UaConnectionWorkspaceContext => context.getEntityType?.() === UA_CONNECTION_ENTITY_TYPE,
);
