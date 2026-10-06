/** Alias of the editor `ua-settings-form` routes bindable, non-text string settings fields to. */
export const BINDABLE_EDITOR_UI_ALIAS = "UmbracoAutomate.PropertyEditorUi.Bindable";

/**
 * Config entries `ua-settings-form` adds alongside the field's own `editorConfig` when it
 * routes a field to the bindable editor.
 */
export const BINDABLE_EDITOR_CONFIG_ALIASES = {
    /** The property editor UI the field declared, which the bindable editor wraps. */
    editorUiAlias: "bindableEditorUiAlias",
    /**
     * Whether the binding switch is offered. Decided once by the form when it routes the field
     * here, so it survives a `visibleWhen` remount that would reset anything the editor itself held.
     */
    bindingSwitchAvailable: "bindingSwitchAvailable",
} as const;
