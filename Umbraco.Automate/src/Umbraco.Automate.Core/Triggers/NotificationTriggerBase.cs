using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Core.Triggers;

/// <summary>
/// Convenience base class for triggers that respond to Umbraco CMS notifications.
/// Combines <see cref="TriggerBase{TSettings,TOutput}"/> metadata with
/// <see cref="INotificationTrigger{TNotification}"/> activation.
/// </summary>
/// <typeparam name="TSettings">The settings POCO type.</typeparam>
/// <typeparam name="TOutput">The output POCO type.</typeparam>
/// <typeparam name="TNotification">The Umbraco notification type.</typeparam>
public abstract class NotificationTriggerBase<TSettings, TOutput, TNotification>
    : TriggerBase<TSettings, TOutput>, INotificationTrigger<TNotification>
    where TSettings : class, new()
    where TOutput : class
    where TNotification : INotification
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationTriggerBase{TSettings, TOutput, TNotification}"/> class.
    /// </summary>
    protected NotificationTriggerBase(TriggerInfrastructure infrastructure) : base(infrastructure)
    {
    }

    /// <inheritdoc />
    public abstract IEnumerable<TriggerEvent> MapEvent(TNotification notification);
}
