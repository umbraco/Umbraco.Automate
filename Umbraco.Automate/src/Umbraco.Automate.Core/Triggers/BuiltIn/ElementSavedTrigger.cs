using UmbracoConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Models;
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
        foreach (var element in notification.SavedEntities.Where(IsEditSave))
        {
            yield return new TriggerEvent<ElementSavedTriggerOutput>
            {
                TriggerAlias = Alias,
                InitiatorType = TriggerInitiatorType.System,
                // Draft saves reuse the same VersionId; UpdateDate is what advances per save,
                // so include it to avoid deduping legitimate sequential saves.
                IdempotencyKey = GenerateIdempotencyKey(element.Key, element.VersionId, element.UpdateDate),
                Output = ToOutput(element, notification.SavedCultures),
            };
        }
    }

    /// <summary>
    /// Whether the save is a create or a content edit rather than a move, trash or restore. The CMS
    /// raises the Saved notification for those too, with ParentId and/or Trashed dirty. A new element's
    /// ParentId is set through the change-tracking setter, so a create always counts as a save.
    /// Limitation: the CMS's WasPropertyDirty also checks the element's own properties
    /// case-insensitively, so on an element type with a property aliased "parentId" or "trashed",
    /// editing that property on an existing element reads as a move and starts no run.
    /// Shared with <see cref="ElementBatchSavedTrigger"/>.
    /// </summary>
    internal static bool IsEditSave(IElement element)
        => IsNew(element)
            || !(element.WasPropertyDirty("ParentId") || element.WasPropertyDirty("Trashed"));

    /// <summary>
    /// Maps a saved element to the output shared by this trigger and <see cref="ElementBatchSavedTrigger"/>.
    /// </summary>
    internal static ElementSavedTriggerOutput ToOutput(
        IElement element,
        IReadOnlyDictionary<Guid, IReadOnlyCollection<string>>? savedCultures)
        => new()
        {
            ElementKey = element.Key,
            ElementName = element.Name,
            ContentTypeKey = element.ContentType?.Key,
            ContentTypeAlias = element.ContentType?.Alias,
            IsNew = IsNew(element),
            Cultures = ContentCultureHelpers.GetSavedCultures(element, savedCultures),
        };

    private static bool IsNew(IElement element) => element.CreateDate == element.UpdateDate;

    /// <inheritdoc />
    protected override bool CanHandle(ElementSavedTriggerOutput output, ElementSavedTriggerSettings? settings)
        => EntityTypesFilter.Matches(output.ContentTypeKey, settings?.ElementTypes);
}
