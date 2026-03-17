import type { UaRunWorkspaceContext } from "./run-workspace.context.js";
import { UmbContextToken } from "@umbraco-cms/backoffice/context-api";
import type { UmbWorkspaceContext } from "@umbraco-cms/backoffice/workspace";
import { UA_RUN_ENTITY_TYPE } from "../../constants.js";

export const UA_RUN_WORKSPACE_CONTEXT = new UmbContextToken<
    UmbWorkspaceContext,
    UaRunWorkspaceContext
>(
    "UmbWorkspaceContext",
    undefined,
    (context): context is UaRunWorkspaceContext => context.getEntityType?.() === UA_RUN_ENTITY_TYPE,
);
