using System.ComponentModel.DataAnnotations;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Notifications.Channels.BuiltIn;

/// <summary>
/// Settings for the webhook notification channel.
/// </summary>
public sealed class WebhookNotificationChannelSettings
{
    /// <summary>
    /// Gets or sets the URL to POST the notification to.
    /// </summary>
    [Field(Label = "URL", Description = "The URL to POST the notification to.")]
    [Required]
    public string? Url { get; set; }

    /// <summary>
    /// Gets or sets an optional shared secret for HMAC-SHA256 signing.
    /// </summary>
    [Field(Label = "Secret", IsSensitive = true, Description = "Optional shared secret for HMAC-SHA256 signing.", SortOrder = 1)]
    public string? Secret { get; set; }
}
