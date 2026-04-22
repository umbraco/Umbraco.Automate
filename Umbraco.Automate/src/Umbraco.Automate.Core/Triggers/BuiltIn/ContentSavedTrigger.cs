using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Fires when content is saved in Umbraco CMS.
/// Produces one <see cref="TriggerEvent"/> per saved content item.
/// </summary>
[Trigger("umbracoAutomate.contentSaved", "Content Saved",
    Description = "Fires when content is saved.",
    Group = "Content",
    Icon = "icon-save")]
public sealed class ContentSavedTrigger
    : NotificationTriggerBase<ContentSavedTriggerSettings, ContentSavedTriggerOutput, ContentSavedNotification>
{
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
                InitiatorType = "system",
                // Draft saves reuse the same VersionId; UpdateDate is what advances per save,
                // so include it to avoid deduping legitimate sequential saves.
                IdempotencyKey = GenerateIdempotencyKey(content.Key, content.VersionId, content.UpdateDate),
                Output = new ContentSavedTriggerOutput
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
    protected override bool CanHandle(ContentSavedTriggerOutput output, ContentSavedTriggerSettings? settings)
        => ContentTypesFilter.Matches(output.ContentTypeKey, settings?.ContentTypes);
}
