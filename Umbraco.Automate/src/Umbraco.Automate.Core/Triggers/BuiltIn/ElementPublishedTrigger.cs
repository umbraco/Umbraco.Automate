using UmbracoConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Fires when a Library element is published in Umbraco CMS.
/// Produces one <see cref="TriggerEvent"/> per published element.
/// </summary>
[Trigger("umbracoAutomate.elementPublished", "Element Published",
    Description = "Fires when a Library element is published.",
    Group = "Library",
    Icon = "icon-globe",
    RequiredSections = [UmbracoConstants.Applications.Library])]
public sealed class ElementPublishedTrigger
    : NotificationTriggerBase<ElementPublishedTriggerSettings, ElementPublishedTriggerOutput, ElementPublishedNotification>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ElementPublishedTrigger"/> class.
    /// </summary>
    public ElementPublishedTrigger(TriggerInfrastructure infrastructure) : base(infrastructure)
    {
    }

    /// <inheritdoc />
    public override IEnumerable<TriggerEvent> MapEvent(ElementPublishedNotification notification)
    {
        foreach (var element in notification.PublishedEntities)
        {
            yield return new TriggerEvent<ElementPublishedTriggerOutput>
            {
                TriggerAlias = Alias,
                InitiatorType = TriggerInitiatorType.System,
                // Key on the version that just got published: rapid successive publishes
                // get distinct PublishedVersionIds and therefore distinct keys, while a
                // duplicate notification for the same publish collapses to one message.
                IdempotencyKey = GenerateIdempotencyKey(element.Key, element.PublishedVersionId),
                Output = new ElementPublishedTriggerOutput
                {
                    ElementKey = element.Key,
                    ElementName = element.Name,
                    ContentTypeKey = element.ContentType?.Key,
                    ContentTypeAlias = element.ContentType?.Alias,
                    Cultures = ContentCultureHelpers.GetPublishedCultures(element, notification.PublishedCultures),
                },
            };
        }
    }

    /// <inheritdoc />
    protected override bool CanHandle(ElementPublishedTriggerOutput output, ElementPublishedTriggerSettings? settings)
        => EntityTypesFilter.Matches(output.ContentTypeKey, settings?.ElementTypes);
}
