using UmbracoConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Fires once when one or more content items are saved, with all items as a collection.
/// Use with ForEach to iterate over all saved items in a single automation run.
/// </summary>
[Trigger("umbracoAutomate.contentBatchSaved", "Content Batch Saved",
    Description = "Fires once when one or more content items are saved, with all items as a collection.",
    Group = "Content",
    Icon = "icon-documents",
    RequiredSections = [UmbracoConstants.Applications.Content])]
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
            IsNew = content.CreateDate == content.UpdateDate,
            Cultures = ContentCultureHelpers.GetSavedCultures(content),
        }).ToList();

        // Draft saves reuse the same VersionId per item; UpdateDate is what advances per save,
        // so include it in the batch hash to avoid deduping legitimate sequential saves.
        var batchIdentity = entities
            .Select(c => (c.Key, c.VersionId, c.UpdateDate))
            .ToList();

        yield return new TriggerEvent<BatchTriggerOutput<ContentSavedTriggerOutput>>
        {
            TriggerAlias = Alias,
            InitiatorType = TriggerInitiatorType.System,
            IdempotencyKey = IdempotencyKeyFactory.ForEntitySaveBatch(Alias, batchIdentity),
            Output = new BatchTriggerOutput<ContentSavedTriggerOutput>
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
    protected override bool CanHandle(BatchTriggerOutput<ContentSavedTriggerOutput> output, ContentSavedTriggerSettings? settings)
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
