import type { ManifestSectionSidebarApp } from "@umbraco-cms/backoffice/section";
import { UA_MENU_ALIAS, UA_SECTION_ALIAS } from "../constants.js";
import { UA_AUTOMATION_ROOT_ENTITY_TYPE } from "../../automation/constants.js";

export const sectionSidebarManifests: ManifestSectionSidebarApp[] = [
    {
        type: "sectionSidebarApp",
        kind: "menuWithEntityActions",
        alias: "UmbracoAutomate.SectionSidebarApp.Menu",
        name: "Automate Section Sidebar",
        weight: 1000,
        meta: {
            label: "Automate",
            menu: UA_MENU_ALIAS,
            entityType: UA_AUTOMATION_ROOT_ENTITY_TYPE,
        },
        conditions: [
            {
                alias: "Umb.Condition.SectionAlias",
                match: UA_SECTION_ALIAS,
            },
        ],
    },
];
