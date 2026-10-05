/** Alias of the editor `ua-settings-form` routes bindable, non-text string settings fields to. */
export const BINDABLE_EDITOR_UI_ALIAS = "UmbracoAutomate.PropertyEditorUi.Bindable";

/**
 * Config entries `ua-settings-form` adds alongside the field's own `editorConfig` when it
 * routes a field to the bindable editor.
 */
export const BINDABLE_EDITOR_CONFIG_ALIASES = {
    /** The property editor UI the field declared, which the bindable editor wraps. */
    editorUiAlias: "bindableEditorUiAlias",
} as const;
