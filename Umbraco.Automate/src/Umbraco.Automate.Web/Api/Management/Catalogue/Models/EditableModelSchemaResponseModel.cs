using System.ComponentModel.DataAnnotations;

namespace Umbraco.Automate.Web.Api.Management.Catalogue.Models;

/// <summary>
/// Serializable response model for an editable model schema.
/// </summary>
public sealed class EditableModelSchemaResponseModel
{
    /// <summary>The ordered list of field descriptors.</summary>
    [Required]
    public IReadOnlyList<EditableModelFieldDescriptorResponseModel> Fields { get; set; } = [];
}

/// <summary>
/// Serializable response model for a single field within a settings schema.
/// </summary>
public sealed class EditableModelFieldDescriptorResponseModel
{
    /// <summary>The property name on the settings POCO.</summary>
    [Required]
    public string PropertyName { get; set; } = string.Empty;

    /// <summary>The display label.</summary>
    [Required]
    public string Label { get; set; } = string.Empty;

    /// <summary>The CLR type name of the property (e.g. "String", "Int32", "Boolean").</summary>
    [Required]
    public string PropertyType { get; set; } = string.Empty;

    /// <summary>Optional description.</summary>
    public string? Description { get; set; }

    /// <summary>The Umbraco editor UI alias, or null to infer from the property type.</summary>
    public string? EditorUiAlias { get; set; }

    /// <summary>The JSON editor configuration, or null for defaults.</summary>
    public string? EditorConfig { get; set; }

    /// <summary>The sort order for display.</summary>
    public int SortOrder { get; set; }

    /// <summary>Whether this field contains sensitive data.</summary>
    public bool IsSensitive { get; set; }

    /// <summary>The UI group name, or null for the default group.</summary>
    public string? Group { get; set; }

    /// <summary>Whether this field is required.</summary>
    public bool IsRequired { get; set; }
}
