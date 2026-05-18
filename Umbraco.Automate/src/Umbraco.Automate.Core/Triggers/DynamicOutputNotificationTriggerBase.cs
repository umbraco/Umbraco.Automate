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
    { }

    /// <inheritdoc />
    public abstract IEnumerable<TriggerEvent> MapEvent(TNotification notification);

    /// <summary>
    /// Generates a deterministic idempotency key for an entity-based trigger event.
    /// A duplicate notification for the same (entity, version) collapses to the same
    /// key and is deduped by the outbox; genuinely separate events produce distinct keys.
    /// </summary>
    /// <param name="entityKey">The Umbraco entity's unique key (content, media, etc.).</param>
    /// <param name="versionId">The CMS version id that represents the event (publish/save/unpublish/trash/delete).</param>
    protected string GenerateIdempotencyKey(Guid entityKey, int versionId)
        => IdempotencyKeyFactory.ForEntityEvent(Alias, entityKey, versionId);
}
