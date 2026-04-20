namespace Umbraco.Automate.Core.Realtime;

/// <summary>
/// A realtime message pushed to any backoffice user currently editing a given content item.
/// </summary>
public sealed class EditorNotificationMessage
{
    /// <summary>
    /// Gets the key of the content item the notification targets.
    /// </summary>
    public required Guid ContentKey { get; init; }

    /// <summary>
    /// Gets the name of the content item at the time the notification was dispatched.
    /// </summary>
    public required string ContentName { get; init; }

    /// <summary>
    /// Gets the message body to show to the editor.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the notification severity. Controls the toast colour in the backoffice.
    /// </summary>
    public EditorNotificationSeverity Severity { get; init; } = EditorNotificationSeverity.Default;
}

/// <summary>
/// Severity level for an <see cref="EditorNotificationMessage"/>, mapped to the backoffice toast colour.
/// </summary>
public enum EditorNotificationSeverity
{
    /// <summary>Neutral / informational.</summary>
    Default,

    /// <summary>Positive / success.</summary>
    Positive,

    /// <summary>Warning.</summary>
    Warning,

    /// <summary>Danger / error.</summary>
    Danger,
}
