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

    /// <summary>
    /// Generates a deterministic idempotency key for a publish/unpublish/trash/delete trigger
    /// event where <paramref name="versionId"/> uniquely identifies the event. A duplicate
    /// notification for the same (entity, version) collapses to the same key and is deduped
    /// by the outbox; genuinely separate events produce distinct keys.
    /// </summary>
    /// <param name="entityKey">The Umbraco entity's unique key (content, media, etc.).</param>
    /// <param name="versionId">The CMS version id — typically <c>IContent.PublishedVersionId</c> or <c>IContentBase.VersionId</c>.</param>
    protected string GenerateIdempotencyKey(Guid entityKey, int versionId)
        => IdempotencyKeyFactory.ForEntityEvent(Alias, entityKey, versionId);

    /// <summary>
    /// Generates a deterministic idempotency key for a save trigger event. Umbraco reuses
    /// the draft version id across successive saves, so <paramref name="updateDate"/>
    /// is included to distinguish legitimate sequential saves while still collapsing a
    /// duplicate notification for the same save.
    /// </summary>
    /// <param name="entityKey">The Umbraco entity's unique key (content, media, etc.).</param>
    /// <param name="versionId">The current CMS version id.</param>
    /// <param name="updateDate">The entity's <c>UpdateDate</c> captured at save time.</param>
    protected string GenerateIdempotencyKey(Guid entityKey, int versionId, DateTime updateDate)
        => IdempotencyKeyFactory.ForEntitySaveEvent(Alias, entityKey, versionId, updateDate);
}
