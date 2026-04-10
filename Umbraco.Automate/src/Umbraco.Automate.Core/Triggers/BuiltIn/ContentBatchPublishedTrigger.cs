using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Fires once when one or more content items are published, with all items as a collection.
/// Use with ForEach to iterate over all published items in a single automation run.
/// </summary>
[Trigger("umbracoAutomate.contentBatchPublished", "Content Batch Published",
    Description = "Fires once when one or more content items are published, with all items as a collection.",
    Group = "Content",
    Icon = "icon-documents")]
public sealed class ContentBatchPublishedTrigger
    : NotificationTriggerBase<ContentPublishedTriggerSettings, BatchTriggerOutput<ContentPublishedTriggerOutput>, ContentPublishedNotification>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContentBatchPublishedTrigger"/> class.
    /// </summary>
    public ContentBatchPublishedTrigger(TriggerInfrastructure infrastructure) : base(infrastructure)
    {
    }

    /// <inheritdoc />
    public override IEnumerable<TriggerEvent> MapEvent(ContentPublishedNotification notification)
    {
        var items = notification.PublishedEntities.Select(content => new ContentPublishedTriggerOutput
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

        yield return new TriggerEvent<BatchTriggerOutput<ContentPublishedTriggerOutput>>
        {
            TriggerAlias = Alias,
            InitiatorType = "system",
            Output = new BatchTriggerOutput<ContentPublishedTriggerOutput>
            {
                Items = items,
                Count = items.Count,
            },
        };
    }
}
