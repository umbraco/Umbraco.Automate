using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Slack.Actions;

/// <summary>
/// Settings for the <see cref="SendMessageAction"/>.
/// </summary>
public sealed class SendMessageSettings
{
    /// <summary>
    /// Gets or sets the Slack channel to post to (e.g. "#general" or a channel ID).
    /// </summary>
    [Field(Label = "Channel", Description = "The channel to post the message to (e.g. #general or a channel ID).")]
    public string Channel { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the message text.
    /// </summary>
    [Field(Label = "Message", Description = "The message text to send.", SortOrder = 1)]
    public string Message { get; set; } = string.Empty;
}
