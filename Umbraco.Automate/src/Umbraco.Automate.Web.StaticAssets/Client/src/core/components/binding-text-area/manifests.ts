export const BINDING_TEXT_AREA_UI_ALIAS = "UmbracoAutomate.PropertyEditorUi.BindingTextArea";

const bindingTextArea: UmbExtensionManifest = {
    type: "propertyEditorUi",
    alias: BINDING_TEXT_AREA_UI_ALIAS,
    name: "Automate Binding Text Area",
    element: () => import("./binding-text-area.element.js"),
    meta: {
        label: "Binding Text Area",
        icon: "icon-code",
        group: "Automate",
    },
};

const insertBindingAction: UmbExtensionManifest = {
    type: "propertyAction",
    kind: "default",
    alias: "UmbracoAutomate.PropertyAction.InsertBinding.TextArea",
    name: "Insert Binding Expression (Text Area)",
    forPropertyEditorUis: [BINDING_TEXT_AREA_UI_ALIAS],
    api: () => import("../binding-text-box/insert-binding.property-action.js"),
    meta: {
        icon: "icon-code",
        label: "Insert binding",
    },
};

export const bindingTextAreaManifests: UmbExtensionManifest[] = [
    bindingTextArea,
    insertBindingAction,
];
