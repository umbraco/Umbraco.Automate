using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Fires when media is permanently deleted in Umbraco CMS (after a hard delete or
/// recycle-bin empty).
/// Produces one <see cref="TriggerEvent"/> per deleted media item.
/// </summary>
[Trigger("umbracoAutomate.mediaDeleted", "Media Deleted",
    Description = "Fires when media is permanently deleted.",
    Group = "Media",
    Icon = "icon-delete")]
public sealed class MediaDeletedTrigger
    : NotificationTriggerBase<MediaDeletedTriggerSettings, MediaDeletedTriggerOutput, MediaDeletedNotification>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MediaDeletedTrigger"/> class.
    /// </summary>
    public MediaDeletedTrigger(TriggerInfrastructure infrastructure) : base(infrastructure)
    {
    }

    /// <inheritdoc />
    public override IEnumerable<TriggerEvent> MapEvent(MediaDeletedNotification notification)
    {
        foreach (var media in notification.DeletedEntities)
        {
            yield return new TriggerEvent<MediaDeletedTriggerOutput>
            {
                TriggerAlias = Alias,
                InitiatorType = TriggerInitiatorType.System,
                // Delete is a single terminal transition — VersionId at time of delete
                // identifies the event, and a duplicate notification carries the same id.
                IdempotencyKey = GenerateIdempotencyKey(media.Key, media.VersionId),
                Output = new MediaDeletedTriggerOutput
                {
                    MediaKey = media.Key,
                    MediaName = media.Name,
                    MediaTypeKey = media.ContentType?.Key,
                    MediaTypeAlias = media.ContentType?.Alias,
                },
            };
        }
    }

    /// <inheritdoc />
    protected override bool CanHandle(MediaDeletedTriggerOutput output, MediaDeletedTriggerSettings? settings)
        => EntityTypesFilter.Matches(output.MediaTypeKey, settings?.MediaTypes);
}
