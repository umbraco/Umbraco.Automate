export const WEBHOOK_METHOD_PICKER_UI_ALIAS = "Umb.Automate.WebhookMethodPicker";

const webhookMethodPicker: UmbExtensionManifest = {
    type: "propertyEditorUi",
    alias: WEBHOOK_METHOD_PICKER_UI_ALIAS,
    name: "Automate Webhook Method Picker",
    element: () => import("./webhook-method-picker.element.js"),
    meta: {
        label: "Webhook Method Picker",
        icon: "icon-arrow-right",
        group: "Automate",
    },
};

export const webhookMethodPickerManifests: UmbExtensionManifest[] = [
    webhookMethodPicker,
];
