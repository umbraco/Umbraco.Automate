export const ELEMENT_KEY_PICKER_UI_ALIAS = "Umb.Automate.ElementKeyPicker";

const elementKeyPicker: UmbExtensionManifest = {
    type: "propertyEditorUi",
    alias: ELEMENT_KEY_PICKER_UI_ALIAS,
    name: "Automate Element Key Picker",
    element: () => import("./element-key-picker.element.js"),
    meta: {
        label: "Element Key Picker",
        icon: "icon-plugin",
        group: "Automate",
    },
};

export const elementKeyPickerManifests: UmbExtensionManifest[] = [elementKeyPicker];
