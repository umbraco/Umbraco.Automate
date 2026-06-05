using UmbracoConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Fires once when one or more media items are saved, with all items as a collection.
/// Use with ForEach to iterate over all saved items in a single automation run.
/// </summary>
[Trigger("umbracoAutomate.mediaBatchSaved", "Media Batch Saved",
    Description = "Fires once when one or more media items are saved, with all items as a collection.",
    Group = "Media",
    Icon = "icon-pictures-alt",
    RequiredSections = [UmbracoConstants.Applications.Media])]
public sealed class MediaBatchSavedTrigger
    : NotificationTriggerBase<MediaSavedTriggerSettings, BatchTriggerOutput<MediaSavedTriggerOutput>, MediaSavedNotification>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MediaBatchSavedTrigger"/> class.
    /// </summary>
    public MediaBatchSavedTrigger(TriggerInfrastructure infrastructure) : base(infrastructure)
    {
    }

    /// <inheritdoc />
    public override IEnumerable<TriggerEvent> MapEvent(MediaSavedNotification notification)
    {
        var entities = notification.SavedEntities.ToList();
        if (entities.Count == 0)
        {
            yield break;
        }

        var items = entities.Select(media => new MediaSavedTriggerOutput
        {
            MediaKey = media.Key,
            MediaName = media.Name,
            MediaTypeKey = media.ContentType?.Key,
            MediaTypeAlias = media.ContentType?.Alias,
            IsNew = media.CreateDate == media.UpdateDate,
        }).ToList();

        // Media saves reuse the same VersionId per item; UpdateDate is what advances per save,
        // so include it in the batch hash to avoid deduping legitimate sequential saves.
        var batchIdentity = entities
            .Select(m => (m.Key, m.VersionId, m.UpdateDate))
            .ToList();

        yield return new TriggerEvent<BatchTriggerOutput<MediaSavedTriggerOutput>>
        {
            TriggerAlias = Alias,
            InitiatorType = TriggerInitiatorType.System,
            IdempotencyKey = IdempotencyKeyFactory.ForEntitySaveBatch(Alias, batchIdentity),
            Output = new BatchTriggerOutput<MediaSavedTriggerOutput>
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
    protected override bool CanHandle(BatchTriggerOutput<MediaSavedTriggerOutput> output, MediaSavedTriggerSettings? settings)
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
