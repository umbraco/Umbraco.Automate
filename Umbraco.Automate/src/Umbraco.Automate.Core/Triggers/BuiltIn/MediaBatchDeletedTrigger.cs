using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Fires once per delete notification with all deleted items as a collection. Provided
/// for parity with the other media batch triggers; in practice Umbraco raises
/// <see cref="MediaDeletedNotification"/> one-per-item so this trigger almost always
/// emits 1-item batches — use <see cref="MediaDeletedTrigger"/> directly unless you want
/// uniform batch-shaped output.
/// </summary>
[Trigger("umbracoAutomate.mediaBatchDeleted", "Media Batch Deleted",
    Description = "Fires once when one or more media items are permanently deleted, with all items as a collection.",
    Group = "Media",
    Icon = "icon-delete")]
public sealed class MediaBatchDeletedTrigger
    : NotificationTriggerBase<MediaDeletedTriggerSettings, BatchTriggerOutput<MediaDeletedTriggerOutput>, MediaDeletedNotification>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MediaBatchDeletedTrigger"/> class.
    /// </summary>
    public MediaBatchDeletedTrigger(TriggerInfrastructure infrastructure) : base(infrastructure)
    {
    }

    /// <inheritdoc />
    public override IEnumerable<TriggerEvent> MapEvent(MediaDeletedNotification notification)
    {
        var entities = notification.DeletedEntities.ToList();
        if (entities.Count == 0)
        {
            yield break;
        }

        var items = entities.Select(media => new MediaDeletedTriggerOutput
        {
            MediaKey = media.Key,
            MediaName = media.Name,
            MediaTypeKey = media.ContentType?.Key,
            MediaTypeAlias = media.ContentType?.Alias,
        }).ToList();

        var batchIdentity = entities
            .Select(m => (m.Key, m.VersionId))
            .ToList();

        yield return new TriggerEvent<BatchTriggerOutput<MediaDeletedTriggerOutput>>
        {
            TriggerAlias = Alias,
            InitiatorType = TriggerInitiatorType.System,
            IdempotencyKey = IdempotencyKeyFactory.ForEntityBatch(Alias, batchIdentity),
            Output = new BatchTriggerOutput<MediaDeletedTriggerOutput>
            {
                Items = items,
                Count = items.Count,
            },
        };
    }

    /// <inheritdoc />
    // Match-if-any semantics: the batch fires whole when at least one item matches the
    // configured filter.
    protected override bool CanHandle(BatchTriggerOutput<MediaDeletedTriggerOutput> output, MediaDeletedTriggerSettings? settings)
    {
        if (string.IsNullOrWhiteSpace(settings?.MediaTypes))
        {
            return true;
        }

        foreach (var item in output.Items)
        {
            if (EntityTypesFilter.Matches(item.MediaTypeKey, settings.MediaTypes))
            {
                return true;
            }
        }

        return false;
    }
}
