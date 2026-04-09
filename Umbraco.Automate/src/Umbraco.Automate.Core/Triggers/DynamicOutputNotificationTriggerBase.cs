using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Core.Triggers;

/// <summary>
/// Convenience base class for notification triggers whose output schema depends on runtime configuration.
/// Combines <see cref="DynamicOutputTriggerBase{TSettings}"/> with
/// <see cref="INotificationTrigger{TNotification}"/> activation.
/// </summary>
/// <typeparam name="TSettings">The settings POCO type.</typeparam>
/// <typeparam name="TNotification">The Umbraco notification type.</typeparam>
public abstract class DynamicOutputNotificationTriggerBase<TSettings, TNotification>
    : DynamicOutputTriggerBase<TSettings>, INotificationTrigger<TNotification>
    where TSettings : class, new()
    where TNotification : INotification
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicOutputNotificationTriggerBase{TSettings, TNotification}"/> class.
    /// </summary>
    protected DynamicOutputNotificationTriggerBase(TriggerInfrastructure infrastructure) : base(infrastructure)
    {
    }

    /// <inheritdoc />
    public abstract IEnumerable<TriggerEvent> MapEvent(TNotification notification);

    /// <summary>
    /// Generates a deterministic idempotency key for a content-based trigger event.
    /// </summary>
    /// <param name="contentKey">The content item's unique key.</param>
    /// <returns>An idempotency key in the format <c>{alias}:{contentKey}:{windowBoundary}</c>.</returns>
    protected string GenerateIdempotencyKey(Guid contentKey)
    {
        var windowMinutes = Infrastructure.DeduplicationOptions.WindowMinutes;
        var boundary = windowMinutes > 0
            ? new DateTime(DateTime.UtcNow.Ticks - (DateTime.UtcNow.Ticks % TimeSpan.FromMinutes(windowMinutes).Ticks), DateTimeKind.Utc).ToString("O")
            : DateTime.UtcNow.ToString("O");

        return $"{Alias}:{contentKey}:{boundary}";
    }
}
