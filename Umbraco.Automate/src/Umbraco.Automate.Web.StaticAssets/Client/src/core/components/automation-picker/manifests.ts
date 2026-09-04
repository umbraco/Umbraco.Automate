export const AUTOMATION_PICKER_UI_ALIAS = "Umb.Automate.AutomationPicker";

const automationPicker: UmbExtensionManifest = {
    type: "propertyEditorUi",
    alias: AUTOMATION_PICKER_UI_ALIAS,
    name: "Automate Automation Picker",
    element: () => import("./automation-picker.element.js"),
    meta: {
        label: "Automation Picker",
        icon: "icon-directions-alt",
        group: "Automate",
    },
};

export const automationPickerManifests: UmbExtensionManifest[] = [automationPicker];
