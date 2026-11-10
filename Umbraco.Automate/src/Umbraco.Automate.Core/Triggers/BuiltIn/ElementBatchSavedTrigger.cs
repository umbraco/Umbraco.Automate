using UmbracoConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Fires once when one or more Library elements are saved, with all items as a collection.
/// Use with ForEach to iterate over all saved items in a single automation run. Elements whose
/// save only moved, trashed or restored them are left out.
/// </summary>
[Trigger("umbracoAutomate.elementBatchSaved", "Element Batch Saved",
    Description = "Fires once when one or more Library elements are saved, with all items as a collection.",
    Group = "Library",
    Icon = "icon-plugin",
    RequiredSections = [UmbracoConstants.Applications.Library])]
public sealed class ElementBatchSavedTrigger
    : NotificationTriggerBase<ElementSavedTriggerSettings, BatchTriggerOutput<ElementSavedTriggerOutput>, ElementSavedNotification>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ElementBatchSavedTrigger"/> class.
    /// </summary>
    public ElementBatchSavedTrigger(TriggerInfrastructure infrastructure) : base(infrastructure)
    {
    }

    /// <inheritdoc />
    public override IEnumerable<TriggerEvent> MapEvent(ElementSavedNotification notification)
    {
        var entities = notification.SavedEntities.Where(ElementSavedTrigger.IsEditSave).ToList();
        if (entities.Count == 0)
        {
            yield break;
        }

        var items = entities
            .Select(element => ElementSavedTrigger.ToOutput(element,notification.SavedCultures))
            .ToList();

        // Draft saves reuse the same VersionId per item; UpdateDate is what advances per save,
        // so include it in the batch hash to avoid deduping legitimate sequential saves.
        var batchIdentity = entities
            .Select(e => (e.Key, e.VersionId, e.UpdateDate))
            .ToList();

        yield return new TriggerEvent<BatchTriggerOutput<ElementSavedTriggerOutput>>
        {
            TriggerAlias = Alias,
            InitiatorType = TriggerInitiatorType.System,
            IdempotencyKey = IdempotencyKeyFactory.ForEntitySaveBatch(Alias, batchIdentity),
            Output = new BatchTriggerOutput<ElementSavedTriggerOutput>
            {
                Items = items,
                Count = items.Count,
            },
        };
    }

    /// <inheritdoc />
    // Match-if-any semantics, as for Content Batch Saved: the batch fires whole when at least
    // one item matches the configured filter.
    protected override bool CanHandle(BatchTriggerOutput<ElementSavedTriggerOutput> output, ElementSavedTriggerSettings? settings)
    {
        if (string.IsNullOrWhiteSpace(settings?.ElementTypes))
        {
            return true;
        }

        foreach (var item in output.Items)
        {
            if (EntityTypesFilter.Matches(item.ContentTypeKey, settings.ElementTypes))
            {
                return true;
            }
        }

        return false;
    }
}
