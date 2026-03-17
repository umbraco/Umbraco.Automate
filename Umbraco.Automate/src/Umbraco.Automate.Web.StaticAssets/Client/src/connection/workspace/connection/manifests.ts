import {
    UmbSubmitWorkspaceAction,
    UMB_WORKSPACE_CONDITION_ALIAS,
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
        weight: 80,
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
];
