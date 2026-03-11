import { sectionMenuManifests } from "./menu/manifests.js";
import { sectionSidebarManifests } from "./sidebar/manifests.js";
import { dashboardManifests } from "./dashboard/manifests.js";
import { UA_SECTION_ALIAS } from "./constants.js";

const section: UmbExtensionManifest = {
    type: "section",
    alias: UA_SECTION_ALIAS,
    name: "Automate Section",
    meta: {
        label: "#uaSections_automate",
        pathname: "automate",
    },
    conditions: [
        {
            alias: "Umb.Condition.SectionUserPermission",
            match: UA_SECTION_ALIAS,
        },
    ],
};

export const sectionManifests: UmbExtensionManifest[] = [
    ...sectionMenuManifests,
    ...sectionSidebarManifests,
    ...dashboardManifests,
    section,
];
