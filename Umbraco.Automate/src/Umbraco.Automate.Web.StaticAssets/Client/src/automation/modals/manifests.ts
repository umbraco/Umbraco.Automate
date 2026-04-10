export const automationModalManifests: Array<UmbExtensionManifest> = [
    {
        type: "modal",
        alias: "UmbracoAutomate.Modal.NodeSettings",
        name: "Automate Node Settings Modal",
        js: () => import("./node-settings/node-settings-modal.element.js"),
    },
    {
        type: "modal",
        alias: "UmbracoAutomate.Modal.TriggerSettings",
        name: "Automate Trigger Settings Modal",
        js: () => import("./trigger-settings/trigger-settings-modal.element.js"),
    },
    {
        type: "modal",
        alias: "UmbracoAutomate.Modal.EdgeFilter",
        name: "Automate Edge Filter Modal",
        js: () => import("./edge-filter/edge-filter-modal.element.js"),
    },
    {
        type: "modal",
        alias: "UmbracoAutomate.Modal.NotificationChannelSettings",
        name: "Automate Notification Channel Settings Modal",
        js: () => import("./notification-channel-settings/notification-channel-settings-modal.element.js"),
    },
];
