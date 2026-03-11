import type { UmbLocalizationDictionary } from "@umbraco-cms/backoffice/localization-api";

export default {
    uaSections: {
        automate: "Automate",
    },
    uaGeneral: {
        select: "Select",
        close: "Close",
        create: "Create",
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
    },
    uaPlaceholders: {
        enterName: "Enter a name...",
        enterAlias: "Enter an alias...",
    },
    uaMenu: {
        automations: "Automations",
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
    uaDashboard: {
        overview: "Overview",
    },
} satisfies UmbLocalizationDictionary;
