export const manifests: UmbExtensionManifest[] = [
    {
        type: "modal",
        alias: "UmbracoAutomate.Modal.Rollback",
        name: "Rollback Modal",
        element: () => import("./rollback-modal/rollback-modal.element.js"),
    },
];
