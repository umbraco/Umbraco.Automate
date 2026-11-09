import { UMB_WORKSPACE_CONDITION_ALIAS } from "@umbraco-cms/backoffice/workspace";
import {
    UA_ANALYTICS_ENTITY_TYPE,
    UA_ANALYTICS_OVERVIEW_VIEW_ALIAS,
    UA_ANALYTICS_WORKSPACE_ALIAS,
} from "../constants.js";

export const analyticsWorkspaceManifests: Array<UmbExtensionManifest> = [
    {
        type: "workspace",
        alias: UA_ANALYTICS_WORKSPACE_ALIAS,
        name: "Analytics Workspace",
        api: () => import("./analytics-workspace.context.js"),
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
