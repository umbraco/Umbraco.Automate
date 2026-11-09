using UmbracoConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Fires when a Library element is unpublished in Umbraco CMS.
/// Produces one <see cref="TriggerEvent"/> per unpublished element.
/// </summary>
[Trigger("umbracoAutomate.elementUnpublished", "Element Unpublished",
    Description = "Fires when a Library element is unpublished.",
    Group = "Library",
    Icon = "icon-globe",
    RequiredSections = [UmbracoConstants.Applications.Library])]
public sealed class ElementUnpublishedTrigger
    : NotificationTriggerBase<ElementUnpublishedTriggerSettings, ElementUnpublishedTriggerOutput, ElementUnpublishedNotification>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ElementUnpublishedTrigger"/> class.
    /// </summary>
    public ElementUnpublishedTrigger(TriggerInfrastructure infrastructure) : base(infrastructure)
    {
    }

    /// <inheritdoc />
    public override IEnumerable<TriggerEvent> MapEvent(ElementUnpublishedNotification notification)
    {
        foreach (var element in notification.UnpublishedEntities)
        {
            yield return new TriggerEvent<ElementUnpublishedTriggerOutput>
            {
                TriggerAlias = Alias,
                InitiatorType = TriggerInitiatorType.System,
                // The element was just unpublished: PublishedVersionId identifies the version
                // that was live up until this event. A duplicate notification for the same
                // unpublish shares this id; a re-publish-then-unpublish produces a new one.
                IdempotencyKey = GenerateIdempotencyKey(element.Key, element.PublishedVersionId),
                Output = new ElementUnpublishedTriggerOutput
                {
                    ElementKey = element.Key,
                    ElementName = element.Name,
                    ContentTypeKey = element.ContentType?.Key,
                    ContentTypeAlias = element.ContentType?.Alias,
                    Cultures = ContentCultureHelpers.GetUnpublishedCultures(element, notification.UnpublishedCultures),
                },
            };
        }
    }

    /// <inheritdoc />
    protected override bool CanHandle(ElementUnpublishedTriggerOutput output, ElementUnpublishedTriggerSettings? settings)
        => EntityTypesFilter.Matches(output.ContentTypeKey, settings?.ElementTypes);
}
