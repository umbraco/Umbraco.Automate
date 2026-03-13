using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Fires when content is published in Umbraco CMS.
/// Produces one <see cref="TriggerEvent"/> per published content item.
/// </summary>
[Trigger("umbracoAutomate.contentPublished", "Content Published")]
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
    public override string? Description => "Fires when content is published.";

    /// <inheritdoc />
    public override string? Group => "Content";

    /// <inheritdoc />
    public override string? Icon => "icon-document";

    /// <inheritdoc />
    public override IEnumerable<TriggerEvent> MapEvent(ContentPublishedNotification notification)
    {
        foreach (var content in notification.PublishedEntities)
        {
            yield return new TriggerEvent<ContentPublishedTriggerOutput>
            {
                TriggerAlias = Alias,
                InitiatorType = "system",
                IdempotencyKey = GenerateIdempotencyKey(content.Key),
                Output = new ContentPublishedTriggerOutput
                {
                    ContentKey = content.Key,
                    ContentName = content.Name,
                    ContentTypeAlias = content.ContentType?.Alias,
                },
            };
        }
    }
}
