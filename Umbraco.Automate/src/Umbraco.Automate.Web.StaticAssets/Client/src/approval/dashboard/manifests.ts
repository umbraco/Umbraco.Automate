import { UA_SECTION_ALIAS } from "../../section/constants.js";

const dashboard: UmbExtensionManifest = {
    type: "dashboard",
    alias: "Ua.Dashboard.Approvals",
    name: "Automate Approvals Dashboard",
    element: () => import("./approval-dashboard.element.js"),
    weight: 4,
    meta: {
        label: "#uaApproval_dashboardTitle",
        pathname: "approvals",
    },
    conditions: [
        {
            alias: "Umb.Condition.SectionAlias",
            match: UA_SECTION_ALIAS,
        },
    ],
};

export const approvalDashboardManifests: UmbExtensionManifest[] = [dashboard];
