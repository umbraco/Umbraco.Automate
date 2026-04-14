using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Fires once when one or more content items are saved, with all items as a collection.
/// Use with ForEach to iterate over all saved items in a single automation run.
/// </summary>
[Trigger("umbracoAutomate.contentBatchSaved", "Content Batch Saved",
    Description = "Fires once when one or more content items are saved, with all items as a collection.",
    Group = "Content",
    Icon = "icon-documents")]
public sealed class ContentBatchSavedTrigger
    : NotificationTriggerBase<ContentSavedTriggerSettings, BatchTriggerOutput<ContentSavedTriggerOutput>, ContentSavedNotification>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContentBatchSavedTrigger"/> class.
    /// </summary>
    public ContentBatchSavedTrigger(TriggerInfrastructure infrastructure) : base(infrastructure)
    {
    }

    /// <inheritdoc />
    public override IEnumerable<TriggerEvent> MapEvent(ContentSavedNotification notification)
    {
        var entities = notification.SavedEntities.ToList();
        if (entities.Count == 0)
        {
            yield break;
        }

        var items = entities.Select(content => new ContentSavedTriggerOutput
        {
            ContentKey = content.Key,
            ContentName = content.Name,
            ContentTypeKey = content.ContentType?.Key,
            ContentTypeAlias = content.ContentType?.Alias,
        }).ToList();

        var batchIdentity = entities
            .Select(c => (c.Key, c.VersionId))
            .ToList();

        yield return new TriggerEvent<BatchTriggerOutput<ContentSavedTriggerOutput>>
        {
            TriggerAlias = Alias,
            InitiatorType = "system",
            IdempotencyKey = IdempotencyKeyFactory.ForContentBatch(Alias, batchIdentity),
            Output = new BatchTriggerOutput<ContentSavedTriggerOutput>
            {
                Items = items,
                Count = items.Count,
            },
        };
    }
}
