export const MEDIA_KEY_PICKER_UI_ALIAS = "Umb.Automate.MediaKeyPicker";

const mediaKeyPicker: UmbExtensionManifest = {
    type: "propertyEditorUi",
    alias: MEDIA_KEY_PICKER_UI_ALIAS,
    name: "Automate Media Key Picker",
    element: () => import("./media-key-picker.element.js"),
    meta: {
        label: "Media Key Picker",
        icon: "icon-picture",
        group: "Automate",
    },
};

export const mediaKeyPickerManifests: UmbExtensionManifest[] = [mediaKeyPicker];
