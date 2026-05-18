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
        var entities = notification.PublishedEntities.ToList();
        if (entities.Count == 0)
        {
            yield break;
        }

        var items = entities.Select(content => new ContentPublishedTriggerOutput
        {
            ContentKey = content.Key,
            ContentName = content.Name,
            ContentTypeKey = content.ContentType?.Key,
            ContentTypeAlias = content.ContentType?.Alias,
        }).ToList();

        // Hash the (key, publishedVersionId) tuples so a duplicate notification for the same
        // batch dedupes; any change in membership or version produces a fresh key.
        var batchIdentity = entities
            .Select(c => (c.Key, c.PublishedVersionId))
            .ToList();

        yield return new TriggerEvent<BatchTriggerOutput<ContentPublishedTriggerOutput>>
        {
            TriggerAlias = Alias,
            InitiatorType = TriggerInitiatorType.System,
            IdempotencyKey = IdempotencyKeyFactory.ForEntityBatch(Alias, batchIdentity),
            Output = new BatchTriggerOutput<ContentPublishedTriggerOutput>
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
    protected override bool CanHandle(BatchTriggerOutput<ContentPublishedTriggerOutput> output, ContentPublishedTriggerSettings? settings)
    {
        if (string.IsNullOrWhiteSpace(settings?.ContentTypes))
        {
            return true;
        }

        foreach (var item in output.Items)
        {
            if (EntityTypesFilter.Matches(item.ContentTypeKey, settings.ContentTypes))
            {
                return true;
            }
        }

        return false;
    }
}
