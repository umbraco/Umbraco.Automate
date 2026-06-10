export const approvalModalManifests: Array<UmbExtensionManifest> = [
    {
        type: "modal",
        alias: "UmbracoAutomate.Modal.ApprovalDecision",
        name: "Automate Approval Decision Modal",
        js: () => import("./approval-decision/approval-decision-modal.element.js"),
    },
];
