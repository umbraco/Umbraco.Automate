namespace Umbraco.Automate.Core.Notifications.Channels;

/// <summary>
/// A channel-agnostic notification message. Callers populate the fields relevant
/// to the channels they target; each channel picks what it needs.
/// </summary>
public sealed class NotificationMessage
{
    /// <summary>
    /// Human-readable subject line. Used by channels that support a subject (e.g. email).
    /// </summary>
    public string? Subject { get; init; }

    /// <summary>
    /// HTML-formatted body content. Used by email channels.
    /// </summary>
    public string? HtmlBody { get; init; }

    /// <summary>
    /// Plain-text body content. Used by channels that prefer or require plain text.
    /// </summary>
    public string? TextBody { get; init; }

    /// <summary>
    /// Structured data payload. Used by webhook channels (JSON-serialized) and
    /// any channel that needs typed data beyond subject/body strings.
    /// </summary>
    public object? Data { get; init; }
}
