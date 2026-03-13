using System.ComponentModel.DataAnnotations;

using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Web.Api.Management.Catalogue.Models;

/// <summary>
/// Response model for a registered connection type.
/// </summary>
public sealed class ConnectionTypeItemResponseModel
{
    /// <summary>The connection type alias.</summary>
    [Required]
    public string Alias { get; set; } = string.Empty;

    /// <summary>The display name.</summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional description.</summary>
    public string? Description { get; set; }

    /// <summary>The category group.</summary>
    public string? Group { get; set; }

    /// <summary>The icon alias.</summary>
    public string? Icon { get; set; }

    /// <summary>The settings schema, or null if no settings.</summary>
    public EditableModelSchema? SettingsSchema { get; set; }
}
