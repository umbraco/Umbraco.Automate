export const KEY_VALUE_EDITOR_UI_ALIAS = "UmbracoAutomate.PropertyEditorUi.KeyValueEditor";

const keyValueEditor: UmbExtensionManifest = {
    type: "propertyEditorUi",
    alias: KEY_VALUE_EDITOR_UI_ALIAS,
    name: "Automate Key Value Editor",
    element: () => import("./key-value-editor.element.js"),
    meta: {
        label: "Key Value Editor",
        icon: "icon-list",
        group: "Automate",
    },
};

export const keyValueEditorManifests: UmbExtensionManifest[] = [keyValueEditor];
