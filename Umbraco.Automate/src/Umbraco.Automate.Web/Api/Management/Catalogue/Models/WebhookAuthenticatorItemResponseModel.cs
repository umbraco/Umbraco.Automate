using System.ComponentModel.DataAnnotations;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Web.Api.Management.Catalogue.Models;

/// <summary>
/// Response model for a registered webhook authenticator (authentication strategy).
/// </summary>
public sealed class WebhookAuthenticatorItemResponseModel
{
    /// <summary>The authenticator alias (e.g. <c>plain-secret</c>, <c>hmac-sha256</c>, <c>github</c>).</summary>
    [Required]
    public string Alias { get; set; } = string.Empty;

    /// <summary>The display name shown in the strategy picker.</summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional short description of how callers must authenticate.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// The strategy-specific settings schema, or null if the authenticator has no settings.
    /// </summary>
    public EditableModelSchema? SettingsSchema { get; set; }
}
