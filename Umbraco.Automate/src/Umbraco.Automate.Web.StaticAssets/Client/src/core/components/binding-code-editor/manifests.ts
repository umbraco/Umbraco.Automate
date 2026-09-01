export const BINDING_CODE_EDITOR_UI_ALIAS = "UmbracoAutomate.PropertyEditorUi.BindingCodeEditor";

const bindingCodeEditor: UmbExtensionManifest = {
    type: "propertyEditorUi",
    alias: BINDING_CODE_EDITOR_UI_ALIAS,
    name: "Automate Binding Code Editor",
    element: () => import("./binding-code-editor.element.js"),
    meta: {
        label: "Binding Code Editor",
        icon: "icon-code",
        group: "Automate",
    },
};

const insertBindingAction: UmbExtensionManifest = {
    type: "propertyAction",
    kind: "default",
    alias: "UmbracoAutomate.PropertyAction.InsertBinding.CodeEditor",
    name: "Insert Binding Expression (Code Editor)",
    forPropertyEditorUis: [BINDING_CODE_EDITOR_UI_ALIAS],
    api: () => import("../binding-text-box/insert-binding.property-action.js"),
    meta: {
        icon: "icon-code",
        label: "Insert binding",
    },
};

export const bindingCodeEditorManifests: UmbExtensionManifest[] = [
    bindingCodeEditor,
    insertBindingAction,
];
