using Umbraco.Automate.Core.Notifications;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models.ServerEvents;
using Umbraco.Cms.Core.ServerEvents;

namespace Umbraco.Automate.Core.Runs;

/// <summary>
/// Bridges automation-run domain notifications onto the Umbraco backoffice
/// server-events SignalR channel so the runs UI updates without a manual reload.
/// </summary>
internal sealed class RunServerEventBridge
    : INotificationAsyncHandler<AutomationRunStartedNotification>,
      INotificationAsyncHandler<AutomationRunCompletedNotification>
{
    /// <summary>
    /// Source identifier surfaced to the backoffice client. Clients subscribe via
    /// <c>byEventSourcesAndEventTypes([RunEventSource], …)</c>.
    /// </summary>
    public const string RunEventSource = "Umbraco:Automate:Run";

    public const string RunStartedEventType = "Started";
    public const string RunUpdatedEventType = "Updated";

    private readonly IServerEventRouter _router;

    public RunServerEventBridge(IServerEventRouter router)
        => _router = router;

    public Task HandleAsync(AutomationRunStartedNotification notification, CancellationToken cancellationToken)
        => _router.BroadcastEventAsync(new ServerEvent
        {
            EventSource = RunEventSource,
            EventType = RunStartedEventType,
            Key = notification.Run.Id,
        });

    public Task HandleAsync(AutomationRunCompletedNotification notification, CancellationToken cancellationToken)
        => _router.BroadcastEventAsync(new ServerEvent
        {
            EventSource = RunEventSource,
            EventType = RunUpdatedEventType,
            Key = notification.Run.Id,
        });
}
