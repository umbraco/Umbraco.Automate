import { UMB_WORKSPACE_CONDITION_ALIAS } from "@umbraco-cms/backoffice/workspace";
import {
    UA_ANALYTICS_ENTITY_TYPE,
    UA_ANALYTICS_OVERVIEW_VIEW_ALIAS,
    UA_ANALYTICS_WORKSPACE_ALIAS,
} from "../constants.js";

export const analyticsWorkspaceManifests: Array<UmbExtensionManifest> = [
    {
        type: "workspace",
        // "default" supplies UMB_WORKSPACE_CONTEXT for the view condition; T13 replaces it with its own api context.
        kind: "default",
        alias: UA_ANALYTICS_WORKSPACE_ALIAS,
        name: "Analytics Workspace",
        element: () => import("./analytics-workspace.element.js"),
        meta: {
            entityType: UA_ANALYTICS_ENTITY_TYPE,
            headline: "#uaMenu_analytics",
        },
    },
    {
        type: "workspaceView",
        alias: UA_ANALYTICS_OVERVIEW_VIEW_ALIAS,
        name: "Analytics Overview Workspace View",
        element: () => import("./views/analytics-overview-view.element.js"),
        weight: 100,
        meta: {
            label: "#uaDashboard_overview",
            pathname: "overview",
            icon: "icon-chart",
        },
        conditions: [
            {
                alias: UMB_WORKSPACE_CONDITION_ALIAS,
                match: UA_ANALYTICS_WORKSPACE_ALIAS,
            },
        ],
    },
];
