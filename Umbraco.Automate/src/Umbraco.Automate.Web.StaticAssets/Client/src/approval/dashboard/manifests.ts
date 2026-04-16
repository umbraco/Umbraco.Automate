import { UA_SECTION_ALIAS } from "../../section/constants.js";
import { UA_WORKSPACES_EXIST_CONDITION_ALIAS } from "../../section/conditions/workspaces-exist.condition.js";

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
        {
            alias: UA_WORKSPACES_EXIST_CONDITION_ALIAS,
        },
    ],
};

export const approvalDashboardManifests: UmbExtensionManifest[] = [dashboard];
