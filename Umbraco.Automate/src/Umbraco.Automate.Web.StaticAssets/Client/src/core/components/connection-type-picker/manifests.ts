export const CONNECTION_TYPE_PICKER_UI_ALIAS = "Umb.Automate.ConnectionTypePicker";

const connectionTypePicker: UmbExtensionManifest = {
    type: "propertyEditorUi",
    alias: CONNECTION_TYPE_PICKER_UI_ALIAS,
    name: "Automate Connection Type Picker",
    element: () => import("./connection-type-picker.element.js"),
    meta: {
        label: "Connection Type Picker",
        icon: "icon-link",
        group: "Automate",
    },
};

export const connectionTypePickerManifests: UmbExtensionManifest[] = [connectionTypePicker];
