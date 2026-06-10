import { UA_WORKSPACE_MGMT_TREE_ALIAS } from "../tree-mgmt/constants.js";
import { UA_SETTINGS_MENU_ALIAS } from "../../section/constants.js";
import { UA_WORKSPACE_MGMT_WORKSPACE_ALIAS, UA_WORKSPACE_MGMT_ROOT_WORKSPACE_ALIAS } from "../workspace/constants.js";
import { UA_WORKSPACE_MGMT_MENU_ITEM_ALIAS } from "./constants.js";

export const workspaceManagementMenuManifests: Array<UmbExtensionManifest> = [
    // Settings sidebar: flat workspace management tree
    {
        type: "menuItem",
        kind: "tree",
        alias: UA_WORKSPACE_MGMT_MENU_ITEM_ALIAS,
        name: "Workspace Management Menu Item",
        weight: 100,
        meta: {
            treeAlias: UA_WORKSPACE_MGMT_TREE_ALIAS,
            label: "#uaMenu_workspaces",
            menus: [UA_SETTINGS_MENU_ALIAS],
            hideTreeRoot: false,
        },
    },
    {
        type: "workspaceContext",
        kind: "menuStructure",
        name: "Workspace Mgmt Menu Structure Workspace Context",
        alias: "UmbracoAutomate.Context.WorkspaceMgmt.Menu.Structure",
        api: () => import("./workspace-menu-structure.context.js"),
        meta: {
            menuItemAlias: UA_WORKSPACE_MGMT_MENU_ITEM_ALIAS,
        },
        conditions: [
            {
                alias: "Umb.Condition.WorkspaceAlias",
                match: UA_WORKSPACE_MGMT_WORKSPACE_ALIAS,
            },
        ],
    },
    {
        type: "workspaceContext",
        kind: "menuStructure",
        name: "Workspace Mgmt Root Menu Structure Workspace Context",
        alias: "UmbracoAutomate.Context.WorkspaceMgmtRoot.Menu.Structure",
        api: () => import("./workspace-menu-structure.context.js"),
        meta: {
            menuItemAlias: UA_WORKSPACE_MGMT_MENU_ITEM_ALIAS,
        },
        conditions: [
            {
                alias: "Umb.Condition.WorkspaceAlias",
                match: UA_WORKSPACE_MGMT_ROOT_WORKSPACE_ALIAS,
            },
        ],
    },
    {
        type: "workspaceFooterApp",
        kind: "menuBreadcrumb",
        alias: "UmbracoAutomate.WorkspaceFooterApp.WorkspaceMgmt.Breadcrumb",
        name: "Workspace Mgmt Breadcrumb Workspace Footer App",
        conditions: [
            {
                alias: "Umb.Condition.WorkspaceAlias",
                match: UA_WORKSPACE_MGMT_WORKSPACE_ALIAS,
            },
        ],
    },
    {
        type: "workspaceFooterApp",
        kind: "menuBreadcrumb",
        alias: "UmbracoAutomate.WorkspaceFooterApp.WorkspaceMgmtRoot.Breadcrumb",
        name: "Workspace Mgmt Root Breadcrumb Workspace Footer App",
        conditions: [
            {
                alias: "Umb.Condition.WorkspaceAlias",
                match: UA_WORKSPACE_MGMT_ROOT_WORKSPACE_ALIAS,
            },
        ],
    },
];
