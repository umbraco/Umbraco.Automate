import { UMB_WORKSPACE_CONDITION_ALIAS } from "@umbraco-cms/backoffice/workspace";
import { UA_RUN_WORKSPACE_ALIAS, UA_RUN_ENTITY_TYPE } from "../../constants.js";

export const manifests: Array<UmbExtensionManifest> = [
    {
        type: "workspace",
        kind: "routable",
        alias: UA_RUN_WORKSPACE_ALIAS,
        name: "Run Workspace",
        api: () => import("./run-workspace.context.js"),
        meta: {
            entityType: UA_RUN_ENTITY_TYPE,
        },
    },
    {
        type: "workspaceView",
        alias: "UmbracoAutomate.Workspace.Run.View.Canvas",
        name: "Run Canvas Workspace View",
        js: () => import("./views/run-canvas-view.element.js"),
        weight: 200,
        meta: {
            label: "Canvas",
            pathname: "canvas",
            icon: "icon-mindmap",
        },
        conditions: [
            {
                alias: UMB_WORKSPACE_CONDITION_ALIAS,
                match: UA_RUN_WORKSPACE_ALIAS,
            },
        ],
    },
    {
        type: "workspaceView",
        alias: "UmbracoAutomate.Workspace.Run.View.Details",
        name: "Run Details Workspace View",
        js: () => import("./views/run-details-view.element.js"),
        weight: 100,
        meta: {
            label: "Details",
            pathname: "details",
            icon: "icon-info",
        },
        conditions: [
            {
                alias: UMB_WORKSPACE_CONDITION_ALIAS,
                match: UA_RUN_WORKSPACE_ALIAS,
            },
        ],
    },
];
