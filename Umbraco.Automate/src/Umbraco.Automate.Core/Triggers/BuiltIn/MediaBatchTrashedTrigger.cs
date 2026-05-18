using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Fires once when one or more media items are moved to the recycle bin, with all items
/// as a collection. Use with ForEach to iterate over all trashed items in a single
/// automation run.
/// </summary>
[Trigger("umbracoAutomate.mediaBatchTrashed", "Media Batch Trashed",
    Description = "Fires once when one or more media items are moved to the recycle bin, with all items as a collection.",
    Group = "Media",
    Icon = "icon-trash")]
public sealed class MediaBatchTrashedTrigger
    : NotificationTriggerBase<MediaTrashedTriggerSettings, BatchTriggerOutput<MediaTrashedTriggerOutput>, MediaMovedToRecycleBinNotification>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MediaBatchTrashedTrigger"/> class.
    /// </summary>
    public MediaBatchTrashedTrigger(TriggerInfrastructure infrastructure) : base(infrastructure)
    {
    }

    /// <inheritdoc />
    public override IEnumerable<TriggerEvent> MapEvent(MediaMovedToRecycleBinNotification notification)
    {
        var infos = notification.MoveInfoCollection.ToList();
        if (infos.Count == 0)
        {
            yield break;
        }

        var items = infos.Select(info => new MediaTrashedTriggerOutput
        {
            MediaKey = info.Entity.Key,
            MediaName = info.Entity.Name,
            MediaTypeKey = info.Entity.ContentType?.Key,
            MediaTypeAlias = info.Entity.ContentType?.Alias,
            OriginalPath = info.OriginalPath,
        }).ToList();

        var batchIdentity = infos
            .Select(info => (info.Entity.Key, info.Entity.VersionId))
            .ToList();

        yield return new TriggerEvent<BatchTriggerOutput<MediaTrashedTriggerOutput>>
        {
            TriggerAlias = Alias,
            InitiatorType = TriggerInitiatorType.System,
            IdempotencyKey = IdempotencyKeyFactory.ForEntityBatch(Alias, batchIdentity),
            Output = new BatchTriggerOutput<MediaTrashedTriggerOutput>
            {
                Items = items,
                Count = items.Count,
            },
        };
    }

    /// <inheritdoc />
    // Match-if-any semantics: the batch fires whole when at least one item matches the
    // configured filter. Automations needing strict per-item filtering should use a
    // filter step inside the workflow.
    protected override bool CanHandle(BatchTriggerOutput<MediaTrashedTriggerOutput> output, MediaTrashedTriggerSettings? settings)
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
