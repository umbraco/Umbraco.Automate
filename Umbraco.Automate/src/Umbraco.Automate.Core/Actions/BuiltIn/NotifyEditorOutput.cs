namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Output produced by the <see cref="NotifyEditorAction"/>.
/// </summary>
public sealed class NotifyEditorOutput
{
    /// <summary>
    /// Gets the key of the content item the notification was dispatched for.
    /// </summary>
    public Guid ContentKey { get; init; }

    /// <summary>
    /// Gets the name of the content item at the time of dispatch.
    /// </summary>
    public string ContentName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the resolved toast title (either the explicit setting or the automation's name fallback).
    /// </summary>
    public string Title { get; init; } = string.Empty;
}
