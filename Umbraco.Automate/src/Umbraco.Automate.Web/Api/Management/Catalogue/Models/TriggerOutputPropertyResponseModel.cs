using System.ComponentModel.DataAnnotations;

namespace Umbraco.Automate.Web.Api.Management.Catalogue.Models;

/// <summary>
/// Serializable response model for a trigger output property.
/// </summary>
public sealed class TriggerOutputPropertyResponseModel
{
    /// <summary>The property name as it appears in expressions.</summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>The CLR type name of the property value (e.g. "String", "Guid").</summary>
    [Required]
    public string Type { get; set; } = string.Empty;

    /// <summary>Optional description of the property.</summary>
    public string? Description { get; set; }
}
