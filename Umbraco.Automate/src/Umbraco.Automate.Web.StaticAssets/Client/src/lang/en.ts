import type { UmbLocalizationDictionary } from "@umbraco-cms/backoffice/localization-api";

export default {
    uaSections: {
        automate: "Automate",
    },
    uaGeneral: {
        select: "Select",
        close: "Close",
        create: "Create",
        createAutomation: "Automation",
        createFolder: "Folder",
        delete: "Delete",
        save: "Save",
        saveAndPublish: "Save and Publish",
        unpublish: "Unpublish",
    },
    uaLabels: {
        name: "Name",
        alias: "Alias",
        status: "Status",
        enabled: "Enabled",
        disabled: "Disabled",
        draft: "Draft",
        published: "Published",
        inactive: "Inactive",
        requestedAt: "Requested At",
    },
    uaPlaceholders: {
        enterName: "Enter a name...",
        enterAlias: "Enter an alias...",
    },
    uaMenu: {
        automations: "Automations",
        settings: "Settings",
        workspaces: "Workspaces",
        connections: "Connections",
    },
    uaAutomation: {
        deleteConfirm: "Are you sure you want to delete this automation?",
        bulkDeleteConfirm: (count: number) =>
            `Are you sure you want to delete ${count} automation(s)?`,
    },
    uaCatalogue: {
        selectTrigger: "Select Trigger",
        selectAction: "Select Action",
        searchPlaceholder: "Search...",
        noResults: "No items found",
        loadError: "Failed to load catalogue items",
    },
    uaSettings: {
        noSettings: "This item has no configurable settings.",
    },
    uaRun: {
        dashboardTitle: "Runs",
        noRuns: "No runs found.",
        noStepRuns: "No step runs recorded for this execution.",
        noRecentActivity: "No recent activity.",
    },
    uaWorkspace: {
        deleteConfirm: "Are you sure you want to delete this workspace?",
        bulkDeleteConfirm: (count: number) =>
            `Are you sure you want to delete ${count} workspace(s)?`,
        serviceAccountKey: "Service Account Key",
        userGroups: "User Groups",
        allowedConnections: "Allowed Connections",
    },
    uaConnection: {
        deleteConfirm: "Are you sure you want to delete this connection?",
        bulkDeleteConfirm: (count: number) =>
            `Are you sure you want to delete ${count} connection(s)?`,
        type: "Type",
        settings: "Settings",
        addSetting: "Add Setting",
        key: "Key",
        value: "Value",
    },
    uaApproval: {
        dashboardTitle: "Approvals",
        approve: "Approve",
        reject: "Reject",
        comment: "Comment",
        commentPlaceholder: "Add an optional comment...",
        promptLabel: "Approval Request",
        noApprovals: "No pending approvals.",
        decisionSuccess: "Decision submitted successfully.",
        decisionError: "Failed to submit decision.",
    },
    uaDashboard: {
        overview: "Overview",
    },
    uaConditionBuilder: {
        addCondition: "Add condition",
        addGroup: "Add group",
        removeCondition: "Remove condition",
        removeGroup: "Remove group",
        leftOperandPlaceholder: "Value or ${binding}",
        rightOperandPlaceholder: "Value",
        and: "AND",
        or: "OR",
    },
    uaSwitchCaseBuilder: {
        addCase: "Add case",
        removeCase: "Remove case",
        caseNamePlaceholder: "e.g. high-priority",
        caseName: "Case name",
        defaultInfo: "Cases are evaluated in order. The first matching case wins. A 'default' outcome handles unmatched conditions.",
    },
    uaVersionHistory: {
        history: "History",
        compare: "Compare",
        compareVersions: (fromVersion: number, toVersion: number) =>
            `Compare version ${fromVersion} with version ${toVersion}`,
        changes: "Changes",
        noChanges: "No changes detected between these versions.",
        noVersionsYet: "No versions yet.",
        rollback: (version: number) => `Rollback to version ${version}`,
    },
} satisfies UmbLocalizationDictionary;
