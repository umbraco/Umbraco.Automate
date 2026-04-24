import {
    UmbSubmitWorkspaceAction,
    UMB_WORKSPACE_CONDITION_ALIAS,
    UMB_WORKSPACE_ENTITY_IS_NEW_CONDITION_ALIAS,
} from "@umbraco-cms/backoffice/workspace";
import { UA_CONNECTION_WORKSPACE_ALIAS, UA_CONNECTION_ENTITY_TYPE } from "../../constants.js";

export const manifests: Array<UmbExtensionManifest> = [
    {
        type: "workspace",
        kind: "routable",
        alias: UA_CONNECTION_WORKSPACE_ALIAS,
        name: "Connection Workspace",
        api: () => import("./connection-workspace.context.js"),
        meta: {
            entityType: UA_CONNECTION_ENTITY_TYPE,
        },
    },
    {
        type: "workspaceView",
        alias: "UmbracoAutomate.Workspace.Connection.View.Settings",
        name: "Connection Settings Workspace View",
        js: () => import("./views/connection-settings-workspace-view.element.js"),
        weight: 200,
        meta: {
            label: "Settings",
            pathname: "settings",
            icon: "icon-settings",
        },
        conditions: [
            {
                alias: UMB_WORKSPACE_CONDITION_ALIAS,
                match: UA_CONNECTION_WORKSPACE_ALIAS,
            },
        ],
    },
    {
        type: "workspaceView",
        alias: "UmbracoAutomate.Workspace.Connection.View.Info",
        name: "Connection Info Workspace View",
        js: () => import("./views/connection-info-workspace-view.element.js"),
        weight: 100,
        meta: {
            label: "Info",
            pathname: "info",
            icon: "icon-info",
        },
        conditions: [
            {
                alias: UMB_WORKSPACE_CONDITION_ALIAS,
                match: UA_CONNECTION_WORKSPACE_ALIAS,
            },
        ],
    },
    {
        type: "workspaceAction",
        kind: "default",
        alias: "UmbracoAutomate.WorkspaceAction.Connection.Save",
        name: "Save Connection Workspace Action",
        weight: 70,
        api: UmbSubmitWorkspaceAction,
        meta: {
            label: "#uaGeneral_save",
            look: "primary",
            color: "positive",
        },
        conditions: [
            {
                alias: UMB_WORKSPACE_CONDITION_ALIAS,
                match: UA_CONNECTION_WORKSPACE_ALIAS,
            },
        ],
    },
    {
        type: "workspaceAction",
        kind: "default",
        alias: "UmbracoAutomate.WorkspaceAction.Connection.Test",
        name: "Test Connection Workspace Action",
        weight: 80,
        api: () => import("./actions/connection-test.action.js"),
        meta: {
            label: "#uaConnection_test",
            look: "secondary",
            color: "default",
        },
        // Only show on saved connections — the server-side test hits a connection by id,
        // so it would always 404 on an unsaved scaffold. Forcing Save first also sidesteps
        // the sensitive-field round-trip (masked fields aren't re-posted during edit).
        conditions: [
            {
                alias: UMB_WORKSPACE_CONDITION_ALIAS,
                match: UA_CONNECTION_WORKSPACE_ALIAS,
            },
            {
                alias: UMB_WORKSPACE_ENTITY_IS_NEW_CONDITION_ALIAS,
                match: false,
            },
        ],
    },
];
