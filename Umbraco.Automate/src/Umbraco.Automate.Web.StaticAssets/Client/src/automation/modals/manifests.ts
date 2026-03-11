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
];
