using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Umbraco.Automate.Core.Settings;

/// <summary>
/// Describes the schema of a settings model for auto-generated UI rendering.
/// Built from <see cref="EditableModelFieldAttribute"/> decorations on a settings POCO.
/// </summary>
public sealed class EditableModelSchema
{
    /// <summary>
    /// Gets the ordered list of field descriptors in this schema.
    /// </summary>
    public required IReadOnlyList<EditableModelFieldDescriptor> Fields { get; init; }
}

/// <summary>
/// Describes a single field within an <see cref="EditableModelSchema"/>.
/// </summary>
public sealed class EditableModelFieldDescriptor
{
    /// <summary>
    /// Gets the property name on the settings POCO.
    /// </summary>
    [JsonIgnore]
    public required string PropertyName { get; init; }

    /// <summary>
    /// Gets the display label.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Gets the CLR type of the property.
    /// </summary>
    [JsonIgnore]
    public required Type PropertyType { get; init; }

    /// <summary>
    /// Gets an optional description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the Umbraco editor UI alias, or null to infer from the property type.
    /// </summary>
    public string? EditorUiAlias { get; init; }

    /// <summary>
    /// Gets the JSON editor configuration, or null for defaults.
    /// </summary>
    public string? EditorConfig { get; init; }

    /// <summary>
    /// Gets the sort order for display.
    /// </summary>
    public int SortOrder { get; init; }

    /// <summary>
    /// Gets whether this field contains sensitive data.
    /// </summary>
    public bool IsSensitive { get; init; }

    /// <summary>
    /// Gets the UI group name, or null for the default group.
    /// </summary>
    public string? Group { get; init; }

    /// <summary>
    /// Gets the validation rules inferred from data annotation attributes on the property.
    /// </summary>
    [JsonIgnore]
    public IEnumerable<ValidationAttribute> ValidationRules { get; init; } = [];
}
