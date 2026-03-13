using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Fires when content is unpublished in Umbraco CMS.
/// Produces one <see cref="TriggerEvent"/> per unpublished content item.
/// </summary>
[Trigger("umbracoAutomate.contentUnpublished", "Content Unpublished")]
public sealed class ContentUnpublishedTrigger
    : NotificationTriggerBase<ContentUnpublishedTriggerSettings, ContentUnpublishedTriggerOutput, ContentUnpublishedNotification>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContentUnpublishedTrigger"/> class.
    /// </summary>
    public ContentUnpublishedTrigger(TriggerInfrastructure infrastructure) : base(infrastructure)
    {
    }

    /// <inheritdoc />
    public override string? Description => "Fires when content is unpublished.";

    /// <inheritdoc />
    public override string? Group => "Content";

    /// <inheritdoc />
    public override string? Icon => "icon-globe";

    /// <inheritdoc />
    public override IEnumerable<TriggerEvent> MapEvent(ContentUnpublishedNotification notification)
    {
        foreach (var content in notification.UnpublishedEntities)
        {
            yield return new TriggerEvent<ContentUnpublishedTriggerOutput>
            {
                TriggerAlias = Alias,
                InitiatorType = "system",
                IdempotencyKey = GenerateIdempotencyKey(content.Key),
                Output = new ContentUnpublishedTriggerOutput
                {
                    ContentKey = content.Key,
                    ContentName = content.Name,
                    ContentTypeAlias = content.ContentType?.Alias,
                },
            };
        }
    }
}
