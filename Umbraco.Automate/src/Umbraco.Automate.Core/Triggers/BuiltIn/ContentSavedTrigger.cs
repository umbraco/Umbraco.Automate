using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Fires when content is saved in Umbraco CMS.
/// Produces one <see cref="TriggerEvent"/> per saved content item.
/// </summary>
[Trigger("umbracoAutomate.contentSaved", "Content Saved")]
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
    public override string? Description => "Fires when content is saved.";

    /// <inheritdoc />
    public override string? Group => "Content";

    /// <inheritdoc />
    public override string? Icon => "icon-save";

    /// <inheritdoc />
    public override IEnumerable<TriggerEvent> MapEvent(ContentSavedNotification notification)
    {
        foreach (var content in notification.SavedEntities)
        {
            yield return new TriggerEvent<ContentSavedTriggerOutput>
            {
                TriggerAlias = Alias,
                InitiatorType = "system",
                IdempotencyKey = GenerateIdempotencyKey(content.Key),
                Output = new ContentSavedTriggerOutput
                {
                    ContentKey = content.Key,
                    ContentName = content.Name,
                    ContentTypeAlias = content.ContentType?.Alias,
                },
            };
        }
    }
}
