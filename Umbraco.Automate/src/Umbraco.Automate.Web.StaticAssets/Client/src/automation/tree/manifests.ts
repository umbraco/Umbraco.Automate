import {
    UA_AUTOMATION_HEALTH_DEGRADED_FLAG,
    UA_AUTOMATION_HEALTH_DISABLED_FLAG,
    UA_AUTOMATION_PENDING_CHANGES_FLAG,
    UA_AUTOMATION_TREE_ALIAS,
    UA_AUTOMATION_TREE_REPOSITORY_ALIAS,
} from "./constants.js";
import { UA_AUTOMATION_ENTITY_TYPE, UA_AUTOMATION_GROUP_ENTITY_TYPE } from "../entity.js";
import { UA_AUTOMATION_GROUP_WORKSPACE_ALIAS } from "../workspace/constants.js";
import { UA_WORKSPACE_VIEW_WORKSPACE_ALIAS } from "../../workspace-management/workspace/constants.js";
import { UA_AUTOMATION_TREE_ITEM_CHILDREN_COLLECTION_ALIAS } from "./tree-item-children/collection/constants.js";
import { manifests as treeItemChildrenManifests } from "./tree-item-children/manifests.js";
import { UMB_WORKSPACE_CONDITION_ALIAS } from "@umbraco-cms/backoffice/workspace";
import { UA_AUTOMATION_ICON } from "../constants.js";

const workspaceView: UmbExtensionManifest = {
    type: "workspaceView",
    kind: "collection",
    alias: "UmbracoAutomate.WorkspaceView.Automation.TreeItemChildrenCollection",
    name: "Automation Tree Item Children Collection Workspace View",
    meta: {
        label: "Automations",
        pathname: "automations",
        icon: UA_AUTOMATION_ICON,
        collectionAlias: UA_AUTOMATION_TREE_ITEM_CHILDREN_COLLECTION_ALIAS,
    },
    conditions: [
        {
            alias: UMB_WORKSPACE_CONDITION_ALIAS,
            oneOf: [UA_WORKSPACE_VIEW_WORKSPACE_ALIAS, UA_AUTOMATION_GROUP_WORKSPACE_ALIAS],
        },
    ],
};

export const automationTreeManifests: Array<UmbExtensionManifest> = [
    {
        type: "repository",
        alias: UA_AUTOMATION_TREE_REPOSITORY_ALIAS,
        name: "Automation Tree Repository",
        api: () => import("./automation-tree.repository.js"),
    },
    {
        type: "tree",
        kind: "default",
        alias: UA_AUTOMATION_TREE_ALIAS,
        name: "Automation Tree",
        api: () => import("./automation-tree.context.js"),
        element: () => import("./automation-tree.element.js"),
        meta: {
            repositoryAlias: UA_AUTOMATION_TREE_REPOSITORY_ALIAS,
        },
    },
    {
        type: "treeItem",
        alias: "UmbracoAutomate.TreeItem.Automation",
        name: "Automation Tree Item",
        forEntityTypes: [UA_AUTOMATION_ENTITY_TYPE],
        element: () => import("./automation-tree-item.element.js"),
    },
    {
        type: "entitySign",
        kind: "icon",
        alias: "UmbracoAutomate.EntitySign.Automation.PendingChanges",
        name: "Automation Pending Changes Entity Sign",
        forEntityTypes: [UA_AUTOMATION_ENTITY_TYPE],
        forEntityFlags: [UA_AUTOMATION_PENDING_CHANGES_FLAG],
        meta: { iconName: "icon-edit", label: "#uaLabels_unpublishedChanges" },
    },
    {
        type: "entitySign",
        kind: "icon",
        alias: "UmbracoAutomate.EntitySign.Automation.HealthDegraded",
        name: "Automation Health Degraded Entity Sign",
        forEntityTypes: [UA_AUTOMATION_ENTITY_TYPE],
        forEntityFlags: [UA_AUTOMATION_HEALTH_DEGRADED_FLAG],
        weight: 900,
        meta: { iconName: "icon-alert", iconColorAlias: "orange", label: "#uaAutomation_degradedBanner" },
    },
    {
        type: "entitySign",
        kind: "icon",
        alias: "UmbracoAutomate.EntitySign.Automation.HealthDisabled",
        name: "Automation Health Disabled Entity Sign",
        forEntityTypes: [UA_AUTOMATION_ENTITY_TYPE],
        forEntityFlags: [UA_AUTOMATION_HEALTH_DISABLED_FLAG],
        weight: 1000,
        meta: { iconName: "icon-block", iconColorAlias: "red", label: "#uaAutomation_disabledBanner" },
    },
    {
        type: "treeItem",
        alias: "UmbracoAutomate.TreeItem.AutomationGroup",
        kind: "default",
        name: "Automation Group Tree Item",
        forEntityTypes: [UA_AUTOMATION_GROUP_ENTITY_TYPE],
    },
    workspaceView,
    ...treeItemChildrenManifests,
];
