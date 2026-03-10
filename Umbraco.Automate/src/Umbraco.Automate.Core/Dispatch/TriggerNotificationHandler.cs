using Umbraco.Automate.Core.Triggers;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Core.Dispatch;

/// <summary>
/// Generic notification handler that bridges Umbraco notifications to trigger dispatch.
/// One instance is auto-registered per distinct notification type discovered at startup.
/// </summary>
/// <typeparam name="TNotification">The Umbraco notification type.</typeparam>
internal sealed class TriggerNotificationHandler<TNotification>(
    IEnumerable<INotificationTrigger<TNotification>> triggers,
    ITriggerDispatcher dispatcher) : INotificationAsyncHandler<TNotification>
    where TNotification : INotification
{
    /// <inheritdoc />
    public async Task HandleAsync(TNotification notification, CancellationToken cancellationToken)
    {
        foreach (var trigger in triggers)
        {
            foreach (var evt in trigger.MapEvent(notification))
            {
                await dispatcher.DispatchAsync(evt, cancellationToken);
            }
        }
    }
}
