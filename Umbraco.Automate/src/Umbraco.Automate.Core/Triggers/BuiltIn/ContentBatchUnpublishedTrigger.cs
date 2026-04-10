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
        var items = notification.UnpublishedEntities.Select(content => new ContentUnpublishedTriggerOutput
        {
            ContentKey = content.Key,
            ContentName = content.Name,
            ContentTypeKey = content.ContentType?.Key,
            ContentTypeAlias = content.ContentType?.Alias,
        }).ToList();

        if (items.Count == 0)
        {
            yield break;
        }

        yield return new TriggerEvent<BatchTriggerOutput<ContentUnpublishedTriggerOutput>>
        {
            TriggerAlias = Alias,
            InitiatorType = "system",
            Output = new BatchTriggerOutput<ContentUnpublishedTriggerOutput>
            {
                Items = items,
                Count = items.Count,
            },
        };
    }
}
