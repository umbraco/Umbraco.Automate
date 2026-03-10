using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Core.Triggers;

/// <summary>
/// Activation interface for triggers that respond to Umbraco CMS notifications.
/// Infrastructure auto-registers a <c>TriggerNotificationHandler&lt;TNotification&gt;</c>
/// for each distinct notification type discovered at startup.
/// </summary>
/// <typeparam name="TNotification">The Umbraco notification type.</typeparam>
public interface INotificationTrigger<in TNotification> : IEventTrigger<TNotification>
    where TNotification : INotification;
