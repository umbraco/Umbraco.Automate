namespace Umbraco.Automate.Core.Settings;

/// <summary>
/// Decorates a settings POCO property with metadata for auto-generated UI rendering.
/// Used on trigger settings, action settings, and connection settings.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class EditableModelFieldAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the display label for the field.
    /// When null, the property name is used as the label.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// Gets or sets the description text for the field.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the Umbraco editor UI alias for rendering the field
    /// (e.g. "Umb.PropertyEditorUi.TextBox", "Umb.PropertyEditorUi.TextArea").
    /// When null, a default editor is inferred from the property type.
    /// </summary>
    public string? EditorUiAlias { get; set; }

    /// <summary>
    /// Gets or sets the JSON configuration for the editor
    /// (e.g. <c>[{ "alias": "rows", "value": 10 }]</c>).
    /// </summary>
    public string? EditorConfig { get; set; }

    /// <summary>
    /// Gets or sets the sort order for displaying fields in the UI.
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the field holds a secret
    /// (an API key, client secret, shared signing key).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Marking a field sensitive has four effects: the value is encrypted at rest, it is masked
    /// in run logs, it is stripped when the owning entity is exported or transferred, and it is
    /// the only kind of field permitted to reference a secret configuration key (see
    /// <c>AutomateOptions.SecretConfigurationKeyPrefixes</c>). It also renders with a masked
    /// editor carrying a reveal toggle, instead of a plain text box.
    /// </para>
    /// <para>
    /// Set <see cref="EditorUiAlias"/> to opt out of the masked editor — masking a multi-line or
    /// structured field, such as a JSON headers blob, would make it unusable. Sensitive values
    /// should be strings; only string values are encrypted.
    /// </para>
    /// </remarks>
    public bool IsSensitive { get; set; }

    /// <summary>
    /// Gets or sets the group name used to visually group related fields in the UI.
    /// Fields with the same group name are rendered together in a separate section.
    /// The value should be a PascalCase identifier (e.g. "Advanced", "Authentication").
    /// </summary>
    public string? Group { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether <c>${ binding }</c> syntax is evaluated
    /// at runtime against automation run data (trigger output, step outputs).
    /// </summary>
    /// <remarks>
    /// Text editors (the default text box, <c>Umb.PropertyEditorUi.TextArea</c>,
    /// <c>Umb.PropertyEditorUi.CodeEditor</c>) gain an "Insert binding" action. Any other
    /// <see cref="EditorUiAlias"/>, such as a content or form picker, keeps its editor and gains
    /// a switch to a binding text box instead, so the field takes either a picked value or a
    /// binding. Only <c>string</c> properties and lists of <c>string</c> are resolved; a binding
    /// on a list is stored as a one-item list and resolves to one string.
    /// </remarks>
    public bool SupportsBindings { get; set; }

    /// <summary>
    /// Gets or sets the name of another property on the same settings model that controls
    /// whether this field applies (e.g. <c>nameof(BodyMode)</c>). Used with
    /// <see cref="VisibleWhenValues"/>; when null the field always applies.
    /// </summary>
    /// <remarks>
    /// A field that does not apply is hidden in the UI and skipped by validation, so a
    /// required field only has to be filled in while it is visible. Its stored value is kept,
    /// so switching back restores what the author had entered.
    /// </remarks>
    public string? VisibleWhen { get; set; }

    /// <summary>
    /// Gets or sets the values of <see cref="VisibleWhen"/> for which this field applies,
    /// compared case-insensitively against the controlling value's string form (an enum
    /// compares by name).
    /// </summary>
    public string[]? VisibleWhenValues { get; set; }
}

/// <summary>
/// Short alias for <see cref="EditableModelFieldAttribute"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class FieldAttribute : EditableModelFieldAttribute;
