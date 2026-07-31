// Kept in step with EditableModelSchemaBuilder's known aliases on the server, which is where
// settings fields opt into these editors via `[Field(EditorUiAlias = ...)]`.
export const CONTENT_KEY_PICKER_UI_ALIAS = "Umb.Automate.ContentKeyPicker";
export const MEDIA_KEY_PICKER_UI_ALIAS = "Umb.Automate.MediaKeyPicker";

const contentKeyPicker: UmbExtensionManifest = {
    type: "propertyEditorUi",
    alias: CONTENT_KEY_PICKER_UI_ALIAS,
    name: "Automate Content Key Picker",
    element: () => import("./content-key-picker.element.js"),
    meta: {
        label: "Content Key Picker",
        icon: "icon-document",
        group: "Automate",
    },
};

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

export const entityKeyPickerManifests: UmbExtensionManifest[] = [
    contentKeyPicker,
    mediaKeyPicker,
];
