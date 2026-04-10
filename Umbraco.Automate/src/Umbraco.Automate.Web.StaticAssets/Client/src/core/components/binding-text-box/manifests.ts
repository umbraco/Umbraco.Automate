export const BINDING_TEXT_BOX_UI_ALIAS = "UmbracoAutomate.PropertyEditorUi.BindingTextBox";

const bindingTextBox: UmbExtensionManifest = {
    type: "propertyEditorUi",
    alias: BINDING_TEXT_BOX_UI_ALIAS,
    name: "Automate Binding Text Box",
    element: () => import("./binding-text-box.element.js"),
    meta: {
        label: "Binding Text Box",
        icon: "icon-code",
        group: "Automate",
    },
};

const insertBindingAction: UmbExtensionManifest = {
    type: "propertyAction",
    kind: "default",
    alias: "UmbracoAutomate.PropertyAction.InsertBinding",
    name: "Insert Binding Expression",
    forPropertyEditorUis: [BINDING_TEXT_BOX_UI_ALIAS],
    api: () => import("./insert-binding.property-action.js"),
    meta: {
        icon: "icon-code",
        label: "Insert binding",
    },
};

const bindingPickerModal: UmbExtensionManifest = {
    type: "modal",
    alias: "Ua.Modal.BindingPicker",
    name: "Binding Picker Modal",
    element: () => import("./binding-picker-modal.element.js"),
};

export const bindingTextBoxManifests: UmbExtensionManifest[] = [
    bindingTextBox,
    insertBindingAction,
    bindingPickerModal,
];
