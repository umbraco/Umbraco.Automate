using UmbracoConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Fires when media is moved to the recycle bin in Umbraco CMS.
/// Produces one <see cref="TriggerEvent"/> per trashed media item.
/// </summary>
[Trigger("umbracoAutomate.mediaTrashed", "Media Trashed",
    Description = "Fires when media is moved to the recycle bin.",
    Group = "Media",
    Icon = "icon-trash",
    RequiredSections = [UmbracoConstants.Applications.Media])]
public sealed class MediaTrashedTrigger
    : NotificationTriggerBase<MediaTrashedTriggerSettings, MediaTrashedTriggerOutput, MediaMovedToRecycleBinNotification>,
      INodeScopedTrigger
{
    // After trashing, the media lives under the recycle bin (path ,-1,-21,...). CMS
    // denies any non-root user access to that path, so a workspace with a restricted start
    // node will be denied here — matching the backoffice UI, where scoped users do not see
    // the recycle bin at all and cannot interact with trashed items even if they originally
    // trashed them.
    NodeScopedTriggerTarget? INodeScopedTrigger.GetTargetNode(object output)
        => output is MediaTrashedTriggerOutput typed
            ? new NodeScopedTriggerTarget(typed.MediaKey, NodeScopedTriggerTargetKind.Media)
            : null;

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
