import type { UaAutomationWorkspaceContext } from "./automation-workspace.context.js";
import { UmbContextToken } from "@umbraco-cms/backoffice/context-api";
import type { UmbSubmittableWorkspaceContext } from "@umbraco-cms/backoffice/workspace";
import { UA_AUTOMATION_ENTITY_TYPE } from "../../constants.js";

export const UA_AUTOMATION_WORKSPACE_CONTEXT = new UmbContextToken<
    UmbSubmittableWorkspaceContext,
    UaAutomationWorkspaceContext
>(
    "UmbWorkspaceContext",
    undefined,
    (context): context is UaAutomationWorkspaceContext => context.getEntityType?.() === UA_AUTOMATION_ENTITY_TYPE,
);
