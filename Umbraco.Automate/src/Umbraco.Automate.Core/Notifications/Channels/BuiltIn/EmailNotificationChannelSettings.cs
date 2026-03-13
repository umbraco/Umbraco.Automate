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

    /// <summary>
    /// Gets or sets an optional subject template. Supports <c>${automationName}</c> and <c>${status}</c> placeholders.
    /// </summary>
    [Field(Label = "Subject template", Description = "Optional. Supports ${automationName} and ${status} placeholders.", SortOrder = 1)]
    public string? SubjectTemplate { get; set; }
}
