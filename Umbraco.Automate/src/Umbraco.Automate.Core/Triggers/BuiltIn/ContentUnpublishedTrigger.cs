using UmbracoConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Fires when content is unpublished in Umbraco CMS.
/// Produces one <see cref="TriggerEvent"/> per unpublished content item.
/// </summary>
[Trigger("umbracoAutomate.contentUnpublished", "Content Unpublished",
    Description = "Fires when content is unpublished.",
    Group = "Content",
    Icon = "icon-globe",
    RequiredSections = [UmbracoConstants.Applications.Content])]
public sealed class ContentUnpublishedTrigger
    : NotificationTriggerBase<ContentUnpublishedTriggerSettings, ContentUnpublishedTriggerOutput, ContentUnpublishedNotification>,
      INodeScopedTrigger
{
    NodeScopedTriggerTarget? INodeScopedTrigger.GetTargetNode(object output)
        => output is ContentUnpublishedTriggerOutput typed
            ? new NodeScopedTriggerTarget(typed.ContentKey, NodeScopedTriggerTargetKind.Content)
            : null;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentUnpublishedTrigger"/> class.
    /// </summary>
    public ContentUnpublishedTrigger(TriggerInfrastructure infrastructure) : base(infrastructure)
    {
    }

    /// <inheritdoc />
    public override IEnumerable<TriggerEvent> MapEvent(ContentUnpublishedNotification notification)
    {
        foreach (var content in notification.UnpublishedEntities)
        {
            yield return new TriggerEvent<ContentUnpublishedTriggerOutput>
            {
                TriggerAlias = Alias,
                InitiatorType = TriggerInitiatorType.System,
                // The content was just unpublished — PublishedVersionId identifies the
                // version that was live up until this event. Two unpublish notifications
                // for the same event share this id; a re-publish-then-unpublish produces
                // a new id, so the second event is not deduped.
                IdempotencyKey = GenerateIdempotencyKey(content.Key, content.PublishedVersionId),
                Output = new ContentUnpublishedTriggerOutput
                {
                    ContentKey = content.Key,
                    ContentName = content.Name,
                    ContentTypeKey = content.ContentType?.Key,
                    ContentTypeAlias = content.ContentType?.Alias,
                },
            };
        }
    }

    /// <inheritdoc />
    protected override bool CanHandle(ContentUnpublishedTriggerOutput output, ContentUnpublishedTriggerSettings? settings)
        => EntityTypesFilter.Matches(output.ContentTypeKey, settings?.ContentTypes);
}
