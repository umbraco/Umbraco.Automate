import { UA_SECTION_ALIAS } from "../constants.js";

const dashboard: UmbExtensionManifest = {
    type: "dashboard",
    alias: "Ua.Dashboard.Automate",
    name: "Automate Overview Dashboard",
    element: () => import("./automate-dashboard.element.js"),
    weight: 10,
    meta: {
        label: "Overview",
        pathname: "overview",
    },
    conditions: [
        {
            alias: "Umb.Condition.SectionAlias",
            match: UA_SECTION_ALIAS,
        },
    ],
};

export const dashboardManifests: UmbExtensionManifest[] = [dashboard];
