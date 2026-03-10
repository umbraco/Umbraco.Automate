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
    /// Gets or sets a value indicating whether the field contains sensitive data.
    /// When true, the value is encrypted at rest and masked in run logs.
    /// </summary>
    public bool IsSensitive { get; set; }

    /// <summary>
    /// Gets or sets the group name used to visually group related fields in the UI.
    /// Fields with the same group name are rendered together in a separate section.
    /// The value should be a PascalCase identifier (e.g. "Advanced", "Authentication").
    /// </summary>
    public string? Group { get; set; }
}

/// <summary>
/// Short alias for <see cref="EditableModelFieldAttribute"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class FieldAttribute : EditableModelFieldAttribute;
