import { UA_WORKSPACE_TREE_ALIAS } from "../../workspace-management/tree/constants.js";
import { UA_WORKSPACE_VIEW_WORKSPACE_ALIAS } from "../../workspace-management/workspace/constants.js";
import { UA_MENU_ALIAS } from "../../section/constants.js";
import {
    UA_AUTOMATION_WORKSPACE_ALIAS,
    UA_AUTOMATION_ROOT_WORKSPACE_ALIAS,
    UA_AUTOMATION_GROUP_WORKSPACE_ALIAS,
} from "../workspace/constants.js";
import { UA_AUTOMATION_MENU_ITEM_ALIAS } from "./constants.js";

export const automationMenuManifests: Array<UmbExtensionManifest> = [
    {
        type: "menuItem",
        kind: "tree",
        alias: UA_AUTOMATION_MENU_ITEM_ALIAS,
        name: "Automations Menu Item",
        weight: 200,
        meta: {
            treeAlias: UA_WORKSPACE_TREE_ALIAS,
            label: "#uaMenu_automations",
            menus: [UA_MENU_ALIAS],
            hideTreeRoot: true,
        },
    },
    {
        type: "workspaceContext",
        kind: "menuStructure",
        name: "Automation Menu Structure Workspace Context",
        alias: "UmbracoAutomate.Context.Automation.Menu.Structure",
        api: () => import("./automation-menu-structure.context.js"),
        meta: {
            menuItemAlias: UA_AUTOMATION_MENU_ITEM_ALIAS,
        },
        conditions: [
            {
                alias: "Umb.Condition.WorkspaceAlias",
                match: UA_AUTOMATION_WORKSPACE_ALIAS,
            },
        ],
    },
    {
        type: "workspaceContext",
        kind: "menuStructure",
        name: "Automation Root Menu Structure Workspace Context",
        alias: "UmbracoAutomate.Context.AutomationRoot.Menu.Structure",
        api: () => import("./automation-menu-structure.context.js"),
        meta: {
            menuItemAlias: UA_AUTOMATION_MENU_ITEM_ALIAS,
        },
        conditions: [
            {
                alias: "Umb.Condition.WorkspaceAlias",
                match: UA_AUTOMATION_ROOT_WORKSPACE_ALIAS,
            },
        ],
    },
    {
        type: "workspaceFooterApp",
        kind: "menuBreadcrumb",
        alias: "UmbracoAutomate.WorkspaceFooterApp.Automation.Breadcrumb",
        name: "Automation Breadcrumb Workspace Footer App",
        conditions: [
            {
                alias: "Umb.Condition.WorkspaceAlias",
                match: UA_AUTOMATION_WORKSPACE_ALIAS,
            },
        ],
    },
    {
        type: "workspaceFooterApp",
        kind: "menuBreadcrumb",
        alias: "UmbracoAutomate.WorkspaceFooterApp.AutomationRoot.Breadcrumb",
        name: "Automation Root Breadcrumb Workspace Footer App",
        conditions: [
            {
                alias: "Umb.Condition.WorkspaceAlias",
                match: UA_AUTOMATION_ROOT_WORKSPACE_ALIAS,
            },
        ],
    },
    {
        type: "workspaceContext",
        kind: "menuStructure",
        name: "Automation Group Menu Structure Workspace Context",
        alias: "UmbracoAutomate.Context.AutomationGroup.Menu.Structure",
        api: () => import("./automation-menu-structure.context.js"),
        meta: {
            menuItemAlias: UA_AUTOMATION_MENU_ITEM_ALIAS,
        },
        conditions: [
            {
                alias: "Umb.Condition.WorkspaceAlias",
                match: UA_AUTOMATION_GROUP_WORKSPACE_ALIAS,
            },
        ],
    },
    {
        type: "workspaceFooterApp",
        kind: "menuBreadcrumb",
        alias: "UmbracoAutomate.WorkspaceFooterApp.AutomationGroup.Breadcrumb",
        name: "Automation Group Breadcrumb Workspace Footer App",
        conditions: [
            {
                alias: "Umb.Condition.WorkspaceAlias",
                match: UA_AUTOMATION_GROUP_WORKSPACE_ALIAS,
            },
        ],
    },
    {
        type: "workspaceContext",
        kind: "menuStructure",
        name: "Workspace View Menu Structure Workspace Context",
        alias: "UmbracoAutomate.Context.WorkspaceView.Menu.Structure",
        api: () => import("./automation-menu-structure.context.js"),
        meta: {
            menuItemAlias: UA_AUTOMATION_MENU_ITEM_ALIAS,
        },
        conditions: [
            {
                alias: "Umb.Condition.WorkspaceAlias",
                match: UA_WORKSPACE_VIEW_WORKSPACE_ALIAS,
            },
        ],
    },
    {
        type: "workspaceFooterApp",
        kind: "menuBreadcrumb",
        alias: "UmbracoAutomate.WorkspaceFooterApp.WorkspaceView.Breadcrumb",
        name: "Workspace View Breadcrumb Workspace Footer App",
        conditions: [
            {
                alias: "Umb.Condition.WorkspaceAlias",
                match: UA_WORKSPACE_VIEW_WORKSPACE_ALIAS,
            },
        ],
    },
];
