namespace Umbraco.Automate.Slack.Actions;

/// <summary>
/// Output produced by the <see cref="SendMessageAction"/>.
/// </summary>
public sealed class SendMessageOutput
{
    /// <summary>
    /// Gets the Slack channel ID the message was posted to.
    /// </summary>
    public string? Channel { get; init; }

    /// <summary>
    /// Gets the Slack message timestamp (unique ID for the message).
    /// </summary>
    public string? Ts { get; init; }
}
