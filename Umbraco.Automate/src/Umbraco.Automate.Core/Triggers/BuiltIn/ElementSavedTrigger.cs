using UmbracoConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Fires when a Library element is saved (covers both new elements and subsequent edits;
/// <see cref="ElementSavedTriggerOutput.IsNew"/> distinguishes them). Saves that only moved,
/// trashed or restored the element are skipped.
/// Produces one <see cref="TriggerEvent"/> per saved element.
/// </summary>
[Trigger("umbracoAutomate.elementSaved", "Element Saved",
    Description = "Fires when a Library element is saved (created or updated).",
    Group = "Library",
    Icon = "icon-save",
    RequiredSections = [UmbracoConstants.Applications.Library])]
public sealed class ElementSavedTrigger
    : NotificationTriggerBase<ElementSavedTriggerSettings, ElementSavedTriggerOutput, ElementSavedNotification>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ElementSavedTrigger"/> class.
    /// </summary>
    public ElementSavedTrigger(TriggerInfrastructure infrastructure) : base(infrastructure)
    {
    }

    /// <inheritdoc />
    public override IEnumerable<TriggerEvent> MapEvent(ElementSavedNotification notification)
    {
        foreach (var element in notification.SavedEntities.Where(ElementSavedTriggerOutput.IsContentSave))
        {
            yield return new TriggerEvent<ElementSavedTriggerOutput>
            {
                TriggerAlias = Alias,
                InitiatorType = TriggerInitiatorType.System,
                // Draft saves reuse the same VersionId; UpdateDate is what advances per save,
                // so include it to avoid deduping legitimate sequential saves.
                IdempotencyKey = GenerateIdempotencyKey(element.Key, element.VersionId, element.UpdateDate),
                Output = ElementSavedTriggerOutput.From(element, notification.SavedCultures),
            };
        }
    }

    /// <inheritdoc />
    protected override bool CanHandle(ElementSavedTriggerOutput output, ElementSavedTriggerSettings? settings)
        => EntityTypesFilter.Matches(output.ContentTypeKey, settings?.ElementTypes);
}
