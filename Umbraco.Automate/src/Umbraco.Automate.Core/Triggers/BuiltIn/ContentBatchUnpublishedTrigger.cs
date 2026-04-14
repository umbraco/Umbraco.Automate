using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Fires once when one or more content items are unpublished, with all items as a collection.
/// Use with ForEach to iterate over all unpublished items in a single automation run.
/// </summary>
[Trigger("umbracoAutomate.contentBatchUnpublished", "Content Batch Unpublished",
    Description = "Fires once when one or more content items are unpublished, with all items as a collection.",
    Group = "Content",
    Icon = "icon-documents")]
public sealed class ContentBatchUnpublishedTrigger
    : NotificationTriggerBase<ContentUnpublishedTriggerSettings, BatchTriggerOutput<ContentUnpublishedTriggerOutput>, ContentUnpublishedNotification>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContentBatchUnpublishedTrigger"/> class.
    /// </summary>
    public ContentBatchUnpublishedTrigger(TriggerInfrastructure infrastructure) : base(infrastructure)
    {
    }

    /// <inheritdoc />
    public override IEnumerable<TriggerEvent> MapEvent(ContentUnpublishedNotification notification)
    {
        var entities = notification.UnpublishedEntities.ToList();
        if (entities.Count == 0)
        {
            yield break;
        }

        var items = entities.Select(content => new ContentUnpublishedTriggerOutput
        {
            ContentKey = content.Key,
            ContentName = content.Name,
            ContentTypeKey = content.ContentType?.Key,
            ContentTypeAlias = content.ContentType?.Alias,
        }).ToList();

        // Use PublishedVersionId to identify what was unpublished — same logic as the
        // single-content unpublish trigger.
        var batchIdentity = entities
            .Select(c => (c.Key, c.PublishedVersionId))
            .ToList();

        yield return new TriggerEvent<BatchTriggerOutput<ContentUnpublishedTriggerOutput>>
        {
            TriggerAlias = Alias,
            InitiatorType = "system",
            IdempotencyKey = IdempotencyKeyFactory.ForContentBatch(Alias, batchIdentity),
            Output = new BatchTriggerOutput<ContentUnpublishedTriggerOutput>
            {
                Items = items,
                Count = items.Count,
            },
        };
    }
}
