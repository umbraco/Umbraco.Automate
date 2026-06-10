import { UA_SECTION_ALIAS } from "../../section/constants.js";
import { UA_WORKSPACES_EXIST_CONDITION_ALIAS } from "../../section/conditions/workspaces-exist.condition.constants.js";

const dashboard: UmbExtensionManifest = {
    type: "dashboard",
    alias: "Ua.Dashboard.Runs",
    name: "Automate Runs Dashboard",
    element: () => import("./run-dashboard.element.js"),
    weight: 5,
    meta: {
        label: "#uaRun_dashboardTitle",
        pathname: "runs",
    },
    conditions: [
        {
            alias: "Umb.Condition.SectionAlias",
            match: UA_SECTION_ALIAS,
        },
        {
            alias: UA_WORKSPACES_EXIST_CONDITION_ALIAS,
        },
    ],
};

export const runDashboardManifests: UmbExtensionManifest[] = [dashboard];
