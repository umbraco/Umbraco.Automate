using Microsoft.AspNetCore.SignalR;
using Umbraco.Automate.Core.Realtime;

namespace Umbraco.Automate.Web.Realtime;

/// <summary>
/// SignalR-backed implementation of <see cref="IEditorNotifier"/> that broadcasts notifications
/// to every connected backoffice client via <see cref="EditorNotificationHub"/>.
/// </summary>
internal sealed class EditorNotifier : IEditorNotifier
{
    private readonly IHubContext<EditorNotificationHub, IEditorNotificationHubClient> _hubContext;

    public EditorNotifier(IHubContext<EditorNotificationHub, IEditorNotificationHubClient> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task NotifyAsync(EditorNotificationMessage message, CancellationToken cancellationToken = default)
        => _hubContext.Clients.All.OnEditorNotification(message);
}
