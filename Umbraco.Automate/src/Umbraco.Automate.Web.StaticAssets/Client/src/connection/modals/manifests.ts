export const manifests: Array<UmbExtensionManifest> = [
    {
        type: "modal",
        alias: "Ua.Modal.ConnectionTypePicker",
        name: "Connection Type Picker Modal",
        js: () => import("./type-picker/connection-type-picker-modal.element.js"),
    },
];
