using Umbraco.Automate.Core.Realtime;

namespace Umbraco.Automate.Web.Realtime;

/// <summary>
/// Strongly-typed client contract for messages sent from <see cref="EditorNotificationHub"/>
/// to the backoffice.
/// </summary>
public interface IEditorNotificationHubClient
{
    /// <summary>
    /// Pushes an editor notification to the connected client.
    /// </summary>
    Task OnEditorNotification(EditorNotificationMessage message);
}
