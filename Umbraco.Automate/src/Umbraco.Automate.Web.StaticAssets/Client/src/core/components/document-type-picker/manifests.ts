export const DOCUMENT_TYPE_PICKER_UI_ALIAS = "Umb.Automate.DocumentTypePicker";

const documentTypePicker: UmbExtensionManifest = {
    type: "propertyEditorUi",
    alias: DOCUMENT_TYPE_PICKER_UI_ALIAS,
    name: "Automate Document Type Picker",
    element: () => import("./document-type-picker.element.js"),
    meta: {
        label: "Document Type Picker",
        icon: "icon-document-dashed-line",
        group: "Automate",
    },
};

export const documentTypePickerManifests: UmbExtensionManifest[] = [documentTypePicker];
