import { UA_SECTION_ALIAS } from "../../section/constants.js";
import { UA_WORKSPACES_EXIST_CONDITION_ALIAS } from "../../section/conditions/workspaces-exist.condition.constants.js";
import {
    UA_ANALYTICS_ENTITY_TYPE,
    UA_ANALYTICS_MENU_ALIAS,
    UA_ANALYTICS_MENU_ITEM_ALIAS,
    UA_ANALYTICS_SIDEBAR_APP_ALIAS,
} from "../constants.js";

export const analyticsMenuManifests: Array<UmbExtensionManifest> = [
    {
        type: "menu",
        alias: UA_ANALYTICS_MENU_ALIAS,
        name: "Automate Monitoring Menu",
    },
    {
        type: "sectionSidebarApp",
        kind: "menu",
        alias: UA_ANALYTICS_SIDEBAR_APP_ALIAS,
        name: "Monitoring Section Sidebar",
        weight: 750,
        meta: {
            label: "#uaMenu_monitoring",
            menu: UA_ANALYTICS_MENU_ALIAS,
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
    },
    {
        type: "menuItem",
        alias: UA_ANALYTICS_MENU_ITEM_ALIAS,
        name: "Analytics Menu Item",
        element: () => import("./analytics-menu-item.element.js"),
        weight: 100,
        meta: {
            label: "#uaMenu_analytics",
            icon: "icon-chart",
            entityType: UA_ANALYTICS_ENTITY_TYPE,
            menus: [UA_ANALYTICS_MENU_ALIAS],
        },
    },
];
