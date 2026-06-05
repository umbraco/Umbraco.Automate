using UmbracoConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Fires when content is published in Umbraco CMS.
/// Produces one <see cref="TriggerEvent"/> per published content item.
/// </summary>
[Trigger("umbracoAutomate.contentPublished", "Content Published",
    Description = "Fires when content is published.",
    Group = "Content",
    Icon = "icon-document",
    RequiredSections = [UmbracoConstants.Applications.Content])]
public sealed class ContentPublishedTrigger
    : NotificationTriggerBase<ContentPublishedTriggerSettings, ContentPublishedTriggerOutput, ContentPublishedNotification>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContentPublishedTrigger"/> class.
    /// </summary>
    public ContentPublishedTrigger(TriggerInfrastructure infrastructure) : base(infrastructure)
    {
    }

    /// <inheritdoc />
    public override IEnumerable<TriggerEvent> MapEvent(ContentPublishedNotification notification)
    {
        foreach (var content in notification.PublishedEntities)
        {
            yield return new TriggerEvent<ContentPublishedTriggerOutput>
            {
                TriggerAlias = Alias,
                InitiatorType = TriggerInitiatorType.System,
                // Key on the version that just got published: rapid successive publishes
                // get distinct PublishedVersionIds and therefore distinct keys, while a
                // duplicate notification for the same publish collapses to one message.
                IdempotencyKey = GenerateIdempotencyKey(content.Key, content.PublishedVersionId),
                Output = new ContentPublishedTriggerOutput
                {
                    ContentKey = content.Key,
                    ContentName = content.Name,
                    ContentTypeKey = content.ContentType?.Key,
                    ContentTypeAlias = content.ContentType?.Alias,
                    Cultures = ContentCultureHelpers.GetPublishedCultures(content),
                },
            };
        }
    }

    /// <inheritdoc />
    protected override bool CanHandle(ContentPublishedTriggerOutput output, ContentPublishedTriggerSettings? settings)
        => EntityTypesFilter.Matches(output.ContentTypeKey, settings?.ContentTypes);
}
