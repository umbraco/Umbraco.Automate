using System.ComponentModel.DataAnnotations;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Notifications.Channels.BuiltIn;

/// <summary>
/// Settings for the email notification channel.
/// </summary>
public sealed class EmailNotificationChannelSettings
{
    /// <summary>
    /// Gets or sets the comma-separated recipient email addresses.
    /// </summary>
    [Field(Label = "Recipients", Description = "Comma-separated email addresses.")]
    [Required]
    public string? Recipients { get; set; }
}
