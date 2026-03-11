export const manifests: Array<UmbExtensionManifest> = [
    {
        type: "modal",
        alias: "UmbracoAutomate.Modal.NodePicker",
        name: "Automate Node Picker Modal",
        js: () => import("./node-picker/node-picker-modal.element.js"),
    },
];
