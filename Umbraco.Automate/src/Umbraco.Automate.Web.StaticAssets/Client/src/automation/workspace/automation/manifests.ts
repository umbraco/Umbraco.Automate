import {
    UmbSubmitWorkspaceAction,
    UMB_WORKSPACE_CONDITION_ALIAS,
    UMB_WORKSPACE_ENTITY_IS_NEW_CONDITION_ALIAS,
} from "@umbraco-cms/backoffice/workspace";
import { UA_AUTOMATION_WORKSPACE_ALIAS, UA_AUTOMATION_ENTITY_TYPE } from "../../constants.js";

export const manifests: Array<UmbExtensionManifest> = [
    {
        type: "workspace",
        kind: "routable",
        alias: UA_AUTOMATION_WORKSPACE_ALIAS,
        name: "Automation Workspace",
        api: () => import("./automation-workspace.context.js"),
        meta: {
            entityType: UA_AUTOMATION_ENTITY_TYPE,
        },
    },
    {
        type: "workspaceView",
        alias: "UmbracoAutomate.Workspace.Automation.View.Workflow",
        name: "Automation Workflow Workspace View",
        js: () => import("./views/automation-workflow-workspace-view.element.js"),
        weight: 200,
        meta: {
            label: "Design",
            pathname: "workflow",
            icon: "icon-mindmap",
        },
        conditions: [
            {
                alias: UMB_WORKSPACE_CONDITION_ALIAS,
                match: UA_AUTOMATION_WORKSPACE_ALIAS,
            },
        ],
    },
    {
        type: "workspaceView",
        alias: "UmbracoAutomate.Workspace.Automation.View.Runs",
        name: "Automation Runs Workspace View",
        js: () => import("./views/automation-runs-workspace-view.element.js"),
        weight: 150,
        meta: {
            label: "Runs",
            pathname: "runs",
            icon: "icon-history",
        },
        conditions: [
            {
                alias: UMB_WORKSPACE_CONDITION_ALIAS,
                match: UA_AUTOMATION_WORKSPACE_ALIAS,
            },
            {
                alias: UMB_WORKSPACE_ENTITY_IS_NEW_CONDITION_ALIAS,
                match: false,
            },
        ],
    },
    {
        type: "workspaceView",
        alias: "UmbracoAutomate.Workspace.Automation.View.Notifications",
        name: "Automation Notifications Workspace View",
        js: () => import("./views/automation-notifications-workspace-view.element.js"),
        weight: 120,
        meta: {
            label: "Notifications",
            pathname: "notifications",
            icon: "icon-message",
        },
        conditions: [
            {
                alias: UMB_WORKSPACE_CONDITION_ALIAS,
                match: UA_AUTOMATION_WORKSPACE_ALIAS,
            },
            {
                alias: UMB_WORKSPACE_ENTITY_IS_NEW_CONDITION_ALIAS,
                match: false,
            },
        ],
    },
    {
        type: "workspaceView",
        alias: "UmbracoAutomate.Workspace.Automation.View.Info",
        name: "Automation Info Workspace View",
        js: () => import("./views/automation-info-workspace-view.element.js"),
        weight: 100,
        meta: {
            label: "Info",
            pathname: "info",
            icon: "icon-info",
        },
        conditions: [
            {
                alias: UMB_WORKSPACE_CONDITION_ALIAS,
                match: UA_AUTOMATION_WORKSPACE_ALIAS,
            },
        ],
    },
    {
        type: "workspaceAction",
        kind: "default",
        alias: "UmbracoAutomate.WorkspaceAction.Automation.Save",
        name: "Save Automation Workspace Action",
        weight: 80,
        api: UmbSubmitWorkspaceAction,
        meta: {
            label: "#uaGeneral_save",
            look: "secondary",
            color: "positive",
        },
        conditions: [
            {
                alias: UMB_WORKSPACE_CONDITION_ALIAS,
                match: UA_AUTOMATION_WORKSPACE_ALIAS,
            },
        ],
    },
    {
        type: "workspaceAction",
        kind: "default",
        alias: "UmbracoAutomate.WorkspaceAction.Automation.SaveAndPublish",
        name: "Save And Publish Automation Workspace Action",
        weight: 70,
        api: () => import("./actions/automation-save-and-publish.action.js"),
        meta: {
            label: "#uaGeneral_saveAndPublish",
            look: "primary",
            color: "positive",
        },
        conditions: [
            {
                alias: UMB_WORKSPACE_CONDITION_ALIAS,
                match: UA_AUTOMATION_WORKSPACE_ALIAS,
            },
        ],
    },
    {
        type: "workspaceActionMenuItem",
        kind: "default",
        alias: "UmbracoAutomate.WorkspaceActionMenuItem.Automation.Import",
        name: "Import Automation Workspace Action Menu Item",
        weight: 5,
        forWorkspaceActions: "UmbracoAutomate.WorkspaceAction.Automation.SaveAndPublish",
        api: () => import("./actions/automation-import.action.js"),
        meta: {
            label: "#uaGeneral_import",
            icon: "icon-download-alt",
        },
        conditions: [
            {
                alias: UMB_WORKSPACE_CONDITION_ALIAS,
                match: UA_AUTOMATION_WORKSPACE_ALIAS,
            },
        ],
    },
    {
        type: "workspaceActionMenuItem",
        kind: "default",
        alias: "UmbracoAutomate.WorkspaceActionMenuItem.Automation.Unpublish",
        name: "Unpublish Automation Workspace Action Menu Item",
        weight: 10,
        forWorkspaceActions: "UmbracoAutomate.WorkspaceAction.Automation.SaveAndPublish",
        api: () => import("./actions/automation-unpublish.action.js"),
        meta: {
            label: "#uaGeneral_unpublish",
            icon: "icon-undo",
        },
        conditions: [
            {
                alias: UMB_WORKSPACE_CONDITION_ALIAS,
                match: UA_AUTOMATION_WORKSPACE_ALIAS,
            },
            {
                alias: UMB_WORKSPACE_ENTITY_IS_NEW_CONDITION_ALIAS,
                match: false,
            },
        ],
    },
];
