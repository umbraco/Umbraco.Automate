import { UA_MENU_ALIAS, UA_SECTION_ALIAS, UA_SETTINGS_MENU_ALIAS } from "../constants.js";
import { UA_AUTOMATION_ROOT_ENTITY_TYPE } from "../../automation/constants.js";

export const sectionSidebarManifests: Array<UmbExtensionManifest> = [
    {
        type: "sectionSidebarApp",
        kind: "menuWithEntityActions",
        alias: "UmbracoAutomate.SectionSidebarApp.Menu",
        name: "Automate Section Sidebar",
        weight: 1000,
        meta: {
            label: "Automations",
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
    {
        type: "menu",
        alias: UA_SETTINGS_MENU_ALIAS,
        name: "Automate Settings Menu",
    },
    {
        type: "sectionSidebarApp",
        kind: "menu",
        alias: "UmbracoAutomate.SectionSidebarApp.Settings",
        name: "Settings Section Sidebar",
        weight: 500,
        meta: {
            label: "#uaMenu_settings",
            menu: UA_SETTINGS_MENU_ALIAS,
        },
        conditions: [
            {
                alias: "Umb.Condition.SectionAlias",
                match: UA_SECTION_ALIAS,
            },
        ],
    },
];
