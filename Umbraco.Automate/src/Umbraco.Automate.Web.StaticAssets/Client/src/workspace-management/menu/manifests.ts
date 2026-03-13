import { UA_WORKSPACE_TREE_ALIAS } from "../tree/constants.js";
import { UA_SETTINGS_MENU_ALIAS } from "../../section/constants.js";
import { UA_WORKSPACE_MGMT_WORKSPACE_ALIAS, UA_WORKSPACE_ROOT_WORKSPACE_ALIAS } from "../workspace/constants.js";
import { UA_WORKSPACE_MENU_ITEM_ALIAS } from "./constants.js";

export const workspaceManagementMenuManifests: Array<UmbExtensionManifest> = [
    {
        type: "menuItem",
        kind: "tree",
        alias: UA_WORKSPACE_MENU_ITEM_ALIAS,
        name: "Workspaces Menu Item",
        weight: 100,
        meta: {
            treeAlias: UA_WORKSPACE_TREE_ALIAS,
            label: "#uaMenu_workspaces",
            menus: [UA_SETTINGS_MENU_ALIAS],
            hideTreeRoot: false,
        },
    },
    {
        type: "workspaceContext",
        kind: "menuStructure",
        name: "Workspace Menu Structure Workspace Context",
        alias: "UmbracoAutomate.Context.Workspace.Menu.Structure",
        api: () => import("./workspace-menu-structure.context.js"),
        meta: {
            menuItemAlias: UA_WORKSPACE_MENU_ITEM_ALIAS,
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
        name: "Workspace Root Menu Structure Workspace Context",
        alias: "UmbracoAutomate.Context.WorkspaceRoot.Menu.Structure",
        api: () => import("./workspace-menu-structure.context.js"),
        meta: {
            menuItemAlias: UA_WORKSPACE_MENU_ITEM_ALIAS,
        },
        conditions: [
            {
                alias: "Umb.Condition.WorkspaceAlias",
                match: UA_WORKSPACE_ROOT_WORKSPACE_ALIAS,
            },
        ],
    },
    {
        type: "workspaceFooterApp",
        kind: "menuBreadcrumb",
        alias: "UmbracoAutomate.WorkspaceFooterApp.Workspace.Breadcrumb",
        name: "Workspace Breadcrumb Workspace Footer App",
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
        alias: "UmbracoAutomate.WorkspaceFooterApp.WorkspaceRoot.Breadcrumb",
        name: "Workspace Root Breadcrumb Workspace Footer App",
        conditions: [
            {
                alias: "Umb.Condition.WorkspaceAlias",
                match: UA_WORKSPACE_ROOT_WORKSPACE_ALIAS,
            },
        ],
    },
];
