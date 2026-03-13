import {
    UmbSubmitWorkspaceAction,
    UMB_WORKSPACE_CONDITION_ALIAS,
} from "@umbraco-cms/backoffice/workspace";
import { UA_WORKSPACE_MGMT_WORKSPACE_ALIAS, UA_WORKSPACE_ENTITY_TYPE } from "../../constants.js";

export const manifests: Array<UmbExtensionManifest> = [
    {
        type: "workspace",
        kind: "routable",
        alias: UA_WORKSPACE_MGMT_WORKSPACE_ALIAS,
        name: "Workspace Management Workspace",
        api: () => import("./workspace-mgmt-workspace.context.js"),
        meta: {
            entityType: UA_WORKSPACE_ENTITY_TYPE,
        },
    },
    {
        type: "workspaceView",
        alias: "UmbracoAutomate.Workspace.WorkspaceMgmt.View.Info",
        name: "Workspace Info Workspace View",
        js: () => import("./views/workspace-info-workspace-view.element.js"),
        weight: 100,
        meta: {
            label: "Info",
            pathname: "info",
            icon: "icon-info",
        },
        conditions: [
            {
                alias: UMB_WORKSPACE_CONDITION_ALIAS,
                match: UA_WORKSPACE_MGMT_WORKSPACE_ALIAS,
            },
        ],
    },
    {
        type: "workspaceAction",
        kind: "default",
        alias: "UmbracoAutomate.WorkspaceAction.WorkspaceMgmt.Save",
        name: "Save Workspace Workspace Action",
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
                match: UA_WORKSPACE_MGMT_WORKSPACE_ALIAS,
            },
        ],
    },
];
