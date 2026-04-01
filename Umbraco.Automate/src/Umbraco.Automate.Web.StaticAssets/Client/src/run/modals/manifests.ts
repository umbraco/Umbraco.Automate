export const manifests: Array<UmbExtensionManifest> = [
    {
        type: "modal",
        alias: "Ua.Modal.RunDetail",
        name: "Run Detail Modal",
        js: () => import("./run-detail-modal.element.js"),
    },
];
