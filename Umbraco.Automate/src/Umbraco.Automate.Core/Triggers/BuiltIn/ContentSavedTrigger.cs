using UmbracoConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Fires when content is saved in Umbraco CMS (covers both new content and subsequent
/// edits — Umbraco raises <see cref="ContentSavedNotification"/> for both, with
/// <see cref="ContentSavedTriggerOutput.IsNew"/> distinguishing them).
/// Produces one <see cref="TriggerEvent"/> per saved content item.
/// </summary>
[Trigger("umbracoAutomate.contentSaved", "Content Saved",
    Description = "Fires when content is saved (created or updated).",
    Group = "Content",
    Icon = "icon-save",
    RequiredSections = [UmbracoConstants.Applications.Content])]
public sealed class ContentSavedTrigger
    : NotificationTriggerBase<ContentSavedTriggerSettings, ContentSavedTriggerOutput, ContentSavedNotification>,
      INodeScopedTrigger
{
    /// <inheritdoc />
    public NodeScopedTriggerTarget? GetTargetNode(object output)
        => output is ContentSavedTriggerOutput typed
            ? new NodeScopedTriggerTarget(typed.ContentKey, NodeScopedTriggerTargetKind.Content)
            : null;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentSavedTrigger"/> class.
    /// </summary>
    public ContentSavedTrigger(TriggerInfrastructure infrastructure) : base(infrastructure)
    {
    }

    /// <inheritdoc />
    public override IEnumerable<TriggerEvent> MapEvent(ContentSavedNotification notification)
    {
        foreach (var content in notification.SavedEntities)
        {
            yield return new TriggerEvent<ContentSavedTriggerOutput>
            {
                TriggerAlias = Alias,
                InitiatorType = TriggerInitiatorType.System,
                // Draft saves reuse the same VersionId; UpdateDate is what advances per save,
                // so include it to avoid deduping legitimate sequential saves.
                IdempotencyKey = GenerateIdempotencyKey(content.Key, content.VersionId, content.UpdateDate),
                Output = new ContentSavedTriggerOutput
                {
                    ContentKey = content.Key,
                    ContentName = content.Name,
                    ContentTypeKey = content.ContentType?.Key,
                    ContentTypeAlias = content.ContentType?.Alias,
                    IsNew = content.CreateDate == content.UpdateDate,
                },
            };
        }
    }

    /// <inheritdoc />
    protected override bool CanHandle(ContentSavedTriggerOutput output, ContentSavedTriggerSettings? settings)
        => EntityTypesFilter.Matches(output.ContentTypeKey, settings?.ContentTypes);
}
