using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Fires when media is moved to the recycle bin in Umbraco CMS.
/// Produces one <see cref="TriggerEvent"/> per trashed media item.
/// </summary>
[Trigger("umbracoAutomate.mediaTrashed", "Media Trashed",
    Description = "Fires when media is moved to the recycle bin.",
    Group = "Media",
    Icon = "icon-trash")]
public sealed class MediaTrashedTrigger
    : NotificationTriggerBase<MediaTrashedTriggerSettings, MediaTrashedTriggerOutput, MediaMovedToRecycleBinNotification>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MediaTrashedTrigger"/> class.
    /// </summary>
    public MediaTrashedTrigger(TriggerInfrastructure infrastructure) : base(infrastructure)
    {
    }

    /// <inheritdoc />
    public override IEnumerable<TriggerEvent> MapEvent(MediaMovedToRecycleBinNotification notification)
    {
        foreach (var info in notification.MoveInfoCollection)
        {
            var media = info.Entity;

            yield return new TriggerEvent<MediaTrashedTriggerOutput>
            {
                TriggerAlias = Alias,
                InitiatorType = TriggerInitiatorType.System,
                // Trash is a once-per-version transition — a duplicate notification carries
                // the same VersionId, while an un-trash followed by re-trash bumps the version.
                IdempotencyKey = GenerateIdempotencyKey(media.Key, media.VersionId),
                Output = new MediaTrashedTriggerOutput
                {
                    MediaKey = media.Key,
                    MediaName = media.Name,
                    MediaTypeKey = media.ContentType?.Key,
                    MediaTypeAlias = media.ContentType?.Alias,
                    OriginalPath = info.OriginalPath,
                },
            };
        }
    }

    /// <inheritdoc />
    protected override bool CanHandle(MediaTrashedTriggerOutput output, MediaTrashedTriggerSettings? settings)
        => EntityTypesFilter.Matches(output.MediaTypeKey, settings?.MediaTypes);
}
