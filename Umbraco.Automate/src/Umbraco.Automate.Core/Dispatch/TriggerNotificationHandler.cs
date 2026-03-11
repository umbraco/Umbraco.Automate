using Umbraco.Automate.Core.Triggers;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Automate.Core.Dispatch;

/// <summary>
/// Generic notification handler that bridges Umbraco notifications to trigger dispatch.
/// One instance is auto-registered per distinct notification type discovered at startup.
/// </summary>
/// <remarks>
/// No server-role filtering here — Umbraco notifications fire in-process on the node that
/// handles the request and do not participate in distributed cache. Every node must capture
/// the event and dispatch it to the message bus. Deduplication is the responsibility of
/// the consumer / execution layer.
/// </remarks>
/// <typeparam name="TNotification">The Umbraco notification type.</typeparam>
internal sealed class TriggerNotificationHandler<TNotification>(
    IEnumerable<INotificationTrigger<TNotification>> triggers,
    ITriggerDispatcher dispatcher,
    IRuntimeState runtimeState) : INotificationAsyncHandler<TNotification>
    where TNotification : INotification
{
    /// <inheritdoc />
    public async Task HandleAsync(TNotification notification, CancellationToken cancellationToken)
    {
        // During install/upgrade, Umbraco fires content notifications (e.g. from package
        // migrations) before Automate's database tables exist. Skip dispatch until the
        // runtime is fully running and migrations have had a chance to complete.
        if (runtimeState.Level != RuntimeLevel.Run)
        {
            return;
        }

        foreach (var trigger in triggers)
        {
            foreach (var evt in trigger.MapEvent(notification))
            {
                await dispatcher.DispatchAsync(evt, cancellationToken);
            }
        }
    }
}
