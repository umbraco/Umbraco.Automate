namespace Umbraco.Automate.Core.Realtime;

/// <summary>
/// Dispatches realtime notifications to backoffice editors. Clients filter by content key,
/// so messages are only shown to users currently editing the matching content item.
/// </summary>
public interface IEditorNotifier
{
    /// <summary>
    /// Broadcasts a notification to all connected backoffice users. The client decides whether
    /// to surface it based on the content the user is currently editing.
    /// </summary>
    Task NotifyAsync(EditorNotificationMessage message, CancellationToken cancellationToken = default);
}
