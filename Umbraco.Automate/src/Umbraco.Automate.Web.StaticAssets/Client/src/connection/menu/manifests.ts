import { UA_CONNECTION_TREE_ALIAS } from "../tree/constants.js";
import { UA_SETTINGS_MENU_ALIAS } from "../../section/constants.js";
import { UA_CONNECTION_WORKSPACE_ALIAS, UA_CONNECTION_ROOT_WORKSPACE_ALIAS } from "../workspace/constants.js";
import { UA_CONNECTION_MENU_ITEM_ALIAS } from "./constants.js";

export const connectionMenuManifests: Array<UmbExtensionManifest> = [
    {
        type: "menuItem",
        kind: "tree",
        alias: UA_CONNECTION_MENU_ITEM_ALIAS,
        name: "Connections Menu Item",
        weight: 50,
        meta: {
            treeAlias: UA_CONNECTION_TREE_ALIAS,
            label: "#uaMenu_connections",
            menus: [UA_SETTINGS_MENU_ALIAS],
            hideTreeRoot: false,
        },
    },
    {
        type: "workspaceContext",
        kind: "menuStructure",
        name: "Connection Menu Structure Workspace Context",
        alias: "UmbracoAutomate.Context.Connection.Menu.Structure",
        api: () => import("./connection-menu-structure.context.js"),
        meta: {
            menuItemAlias: UA_CONNECTION_MENU_ITEM_ALIAS,
        },
        conditions: [
            {
                alias: "Umb.Condition.WorkspaceAlias",
                match: UA_CONNECTION_WORKSPACE_ALIAS,
            },
        ],
    },
    {
        type: "workspaceContext",
        kind: "menuStructure",
        name: "Connection Root Menu Structure Workspace Context",
        alias: "UmbracoAutomate.Context.ConnectionRoot.Menu.Structure",
        api: () => import("./connection-menu-structure.context.js"),
        meta: {
            menuItemAlias: UA_CONNECTION_MENU_ITEM_ALIAS,
        },
        conditions: [
            {
                alias: "Umb.Condition.WorkspaceAlias",
                match: UA_CONNECTION_ROOT_WORKSPACE_ALIAS,
            },
        ],
    },
    {
        type: "workspaceFooterApp",
        kind: "menuBreadcrumb",
        alias: "UmbracoAutomate.WorkspaceFooterApp.Connection.Breadcrumb",
        name: "Connection Breadcrumb Workspace Footer App",
        conditions: [
            {
                alias: "Umb.Condition.WorkspaceAlias",
                match: UA_CONNECTION_WORKSPACE_ALIAS,
            },
        ],
    },
    {
        type: "workspaceFooterApp",
        kind: "menuBreadcrumb",
        alias: "UmbracoAutomate.WorkspaceFooterApp.ConnectionRoot.Breadcrumb",
        name: "Connection Root Breadcrumb Workspace Footer App",
        conditions: [
            {
                alias: "Umb.Condition.WorkspaceAlias",
                match: UA_CONNECTION_ROOT_WORKSPACE_ALIAS,
            },
        ],
    },
];
