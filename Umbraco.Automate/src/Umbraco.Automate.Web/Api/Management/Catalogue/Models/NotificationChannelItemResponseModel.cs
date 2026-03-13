using System.ComponentModel.DataAnnotations;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Web.Api.Management.Catalogue.Models;

/// <summary>
/// Response model for a registered notification channel type.
/// </summary>
public sealed class NotificationChannelItemResponseModel
{
    /// <summary>The channel alias.</summary>
    [Required]
    public string Alias { get; set; } = string.Empty;

    /// <summary>The display name.</summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional description.</summary>
    public string? Description { get; set; }

    /// <summary>The icon alias.</summary>
    public string? Icon { get; set; }

    /// <summary>The settings schema, or null if no settings.</summary>
    public Core.Settings.EditableModelSchema? SettingsSchema { get; set; }
}
