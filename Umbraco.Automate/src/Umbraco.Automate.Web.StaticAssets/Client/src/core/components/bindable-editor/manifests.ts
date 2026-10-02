import { BINDABLE_EDITOR_UI_ALIAS } from "./constants.js";

export { BINDABLE_EDITOR_CONFIG_ALIASES, BINDABLE_EDITOR_UI_ALIAS } from "./constants.js";

const bindableEditor: UmbExtensionManifest = {
    type: "propertyEditorUi",
    alias: BINDABLE_EDITOR_UI_ALIAS,
    name: "Automate Bindable Editor",
    element: () => import("./bindable-editor.element.js"),
    meta: {
        label: "Bindable Editor",
        icon: "icon-code",
        group: "Automate",
    },
};

// Same action as the binding text fields, so bindings are entered the same way everywhere.
// From the wrapped editor it also switches the field into binding mode.
const insertBindingAction: UmbExtensionManifest = {
    type: "propertyAction",
    kind: "default",
    alias: "UmbracoAutomate.PropertyAction.InsertBinding.Bindable",
    name: "Insert Binding Expression (Bindable Editor)",
    forPropertyEditorUis: [BINDABLE_EDITOR_UI_ALIAS],
    api: () => import("../binding-text-box/insert-binding.property-action.js"),
    meta: {
        icon: "icon-code",
        label: "Insert binding",
    },
};

export const bindableEditorManifests: UmbExtensionManifest[] = [bindableEditor, insertBindingAction];
