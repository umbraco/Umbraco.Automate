import type { UaAnalyticsWorkspaceContext } from "./analytics-workspace.context.js";
import { UmbContextToken } from "@umbraco-cms/backoffice/context-api";
import type { UmbWorkspaceContext } from "@umbraco-cms/backoffice/workspace";
import { UA_ANALYTICS_ENTITY_TYPE } from "../constants.js";

export const UA_ANALYTICS_WORKSPACE_CONTEXT = new UmbContextToken<UmbWorkspaceContext, UaAnalyticsWorkspaceContext>(
    "UmbWorkspaceContext",
    undefined,
    (context): context is UaAnalyticsWorkspaceContext => context.getEntityType?.() === UA_ANALYTICS_ENTITY_TYPE,
);
