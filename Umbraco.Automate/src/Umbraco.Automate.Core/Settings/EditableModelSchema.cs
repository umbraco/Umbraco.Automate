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
    /// The type of the editable model.
    /// </summary>
    [JsonIgnore]
    public Type? Type { get; set; }

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
    /// The unique key identifying the setting.
    /// </summary>
    [Required]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets the display label.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Gets an optional description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the property name on the settings POCO.
    /// </summary>
    [JsonIgnore]
    public string PropertyName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the CLR type of the property.
    /// </summary>
    [JsonIgnore]
    public Type PropertyType { get; init; } = null!;

    /// <summary>
    /// Gets whether the field holds a string, another single value or a collection.
    /// Derived from the property's CLR type.
    /// </summary>
    public EditableModelValueKind ValueKind { get; init; }

    /// <summary>
    /// Gets the Umbraco editor UI alias. Set from <see cref="EditableModelFieldAttribute.EditorUiAlias"/>
    /// when provided, otherwise inferred from the CLR property type by the schema builder.
    /// </summary>
    public string? EditorUiAlias { get; init; }

    /// <summary>
    /// Gets the JSON editor configuration, or null for defaults.
    /// </summary>
    public string? EditorConfig { get; init; }

    /// <summary>
    /// Gets the default value of the property from the model's initializer.
    /// </summary>
    public object? DefaultValue { get; init; }

    /// <summary>
    /// Gets the sort order for display.
    /// </summary>
    public int SortOrder { get; init; }

    /// <summary>
    /// Gets whether this field holds a secret. See
    /// <see cref="EditableModelFieldAttribute.IsSensitive"/> for what that implies. Rendering
    /// does not read this — the masked editor is already baked into
    /// <see cref="EditorUiAlias"/> by the schema builder.
    /// </summary>
    public bool IsSensitive { get; init; }

    /// <summary>
    /// Whether this setting is required.
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Gets the UI group name, or null for the default group.
    /// </summary>
    public string? Group { get; init; }

    /// <summary>
    /// Gets a value indicating whether <c>${ binding }</c> syntax is evaluated at runtime.
    /// </summary>
    public bool SupportsBindings { get; init; }

    /// <summary>
    /// Gets the condition under which this field applies, or null when it always applies.
    /// See <see cref="EditableModelFieldAttribute.VisibleWhen"/>.
    /// </summary>
    public EditableModelFieldVisibility? VisibleWhen { get; init; }

    /// <summary>
    /// Gets the validation rules inferred from data annotation attributes on the property.
    /// </summary>
    [JsonIgnore]
    public IEnumerable<ValidationAttribute> ValidationRules { get; init; } = [];
}

/// <summary>
/// Makes an <see cref="EditableModelFieldDescriptor"/> apply only while another field on the
/// same model holds one of <see cref="Values"/>.
/// </summary>
public sealed class EditableModelFieldVisibility
{
    /// <summary>
    /// Gets the key of the controlling field.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Gets the property name of the controlling field on the settings POCO.
    /// </summary>
    [JsonIgnore]
    public string PropertyName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the controlling values for which the field applies, compared case-insensitively.
    /// </summary>
    public required IReadOnlyList<string> Values { get; init; }

    /// <summary>
    /// Determines whether the field applies for the given controlling value.
    /// </summary>
    /// <param name="controllingValue">The current value of the controlling field.</param>
    /// <returns><c>true</c> when the value's string form matches one of <see cref="Values"/>.</returns>
    public bool IsVisibleFor(object? controllingValue)
    {
        var text = controllingValue?.ToString();
        return text is not null && Values.Contains(text, StringComparer.OrdinalIgnoreCase);
    }
}
