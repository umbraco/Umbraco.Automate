export const WEBHOOK_AUTHENTICATOR_PICKER_UI_ALIAS = "Umb.Automate.WebhookAuthenticatorPicker";

const webhookAuthenticatorPicker: UmbExtensionManifest = {
    type: "propertyEditorUi",
    alias: WEBHOOK_AUTHENTICATOR_PICKER_UI_ALIAS,
    name: "Automate Webhook Authenticator Picker",
    element: () => import("./webhook-authenticator-picker.element.js"),
    meta: {
        label: "Webhook Authenticator Picker",
        icon: "icon-lock",
        group: "Automate",
    },
};

export const webhookAuthenticatorPickerManifests: UmbExtensionManifest[] = [
    webhookAuthenticatorPicker,
];
