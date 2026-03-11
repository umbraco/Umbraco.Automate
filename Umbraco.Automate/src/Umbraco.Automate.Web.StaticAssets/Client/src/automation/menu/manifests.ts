import { UA_AUTOMATION_TREE_ALIAS } from "../tree/constants.js";
import { UA_MENU_ALIAS } from "../../section/constants.js";

export const automationMenuManifests: Array<UmbExtensionManifest> = [
    {
        type: "menuItem",
        kind: "tree",
        alias: "UmbracoAutomate.MenuItem.Automations",
        name: "Automations Menu Item",
        weight: 200,
        meta: {
            treeAlias: UA_AUTOMATION_TREE_ALIAS,
            label: "#uaMenu_automations",
            menus: [UA_MENU_ALIAS],
            hideTreeRoot: true,
        },
    },
];
