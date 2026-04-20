export const EDITOR_NOTIFICATION_SEVERITY_PICKER_UI_ALIAS = "Umb.Automate.EditorNotificationSeverityPicker";

const editorNotificationSeverityPicker: UmbExtensionManifest = {
    type: "propertyEditorUi",
    alias: EDITOR_NOTIFICATION_SEVERITY_PICKER_UI_ALIAS,
    name: "Automate Editor Notification Severity Picker",
    element: () => import("./editor-notification-severity-picker.element.js"),
    meta: {
        label: "Editor Notification Severity Picker",
        icon: "icon-megaphone",
        group: "Automate",
    },
};

export const editorNotificationSeverityPickerManifests: UmbExtensionManifest[] = [
    editorNotificationSeverityPicker,
];
