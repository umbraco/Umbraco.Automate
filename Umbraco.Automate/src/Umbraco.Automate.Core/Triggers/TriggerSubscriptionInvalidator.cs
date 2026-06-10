using Umbraco.Automate.Core.Notifications;
using Umbraco.Cms.Core.Events;

namespace Umbraco.Automate.Core.Triggers;

/// <summary>
/// Listens for automation lifecycle notifications and invalidates the
/// <see cref="ITriggerSubscriptionRegistry"/> cache so that the next subscriber lookup
/// reflects the new state.
/// </summary>
internal sealed class TriggerSubscriptionInvalidator(ITriggerSubscriptionRegistry registry) :
    INotificationAsyncHandler<AutomationSavedNotification>,
    INotificationAsyncHandler<AutomationPublishedNotification>,
    INotificationAsyncHandler<AutomationUnpublishedNotification>,
    INotificationAsyncHandler<AutomationDeletedNotification>
{
    public Task HandleAsync(AutomationSavedNotification notification, CancellationToken cancellationToken)
    {
        registry.Invalidate();
        return Task.CompletedTask;
    }

    public Task HandleAsync(AutomationPublishedNotification notification, CancellationToken cancellationToken)
    {
        registry.Invalidate();
        return Task.CompletedTask;
    }

    public Task HandleAsync(AutomationUnpublishedNotification notification, CancellationToken cancellationToken)
    {
        registry.Invalidate();
        return Task.CompletedTask;
    }

    public Task HandleAsync(AutomationDeletedNotification notification, CancellationToken cancellationToken)
    {
        registry.Invalidate();
        return Task.CompletedTask;
    }
}
