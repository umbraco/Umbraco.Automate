import { UA_SECTION_ALIAS } from "../../section/constants.js";

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
    ],
};

export const runDashboardManifests: UmbExtensionManifest[] = [dashboard];
