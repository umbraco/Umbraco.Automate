export const WEBHOOK_SECRET_FIELD_UI_ALIAS = "Umb.Automate.WebhookSecretField";

const webhookSecretField: UmbExtensionManifest = {
    type: "propertyEditorUi",
    alias: WEBHOOK_SECRET_FIELD_UI_ALIAS,
    name: "Automate Webhook Secret Field",
    element: () => import("./webhook-secret-field.element.js"),
    meta: {
        label: "Webhook Secret Field",
        icon: "icon-lock",
        group: "Automate",
    },
};

export const webhookSecretFieldManifests: UmbExtensionManifest[] = [
    webhookSecretField,
];
