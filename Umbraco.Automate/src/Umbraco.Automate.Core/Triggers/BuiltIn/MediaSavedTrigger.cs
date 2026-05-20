using UmbracoConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Fires when media is saved in Umbraco CMS (covers both new uploads and subsequent edits —
/// Umbraco raises <see cref="MediaSavedNotification"/> for both, with
/// <see cref="MediaSavedTriggerOutput.IsNew"/> distinguishing them).
/// Produces one <see cref="TriggerEvent"/> per saved media item.
/// </summary>
[Trigger("umbracoAutomate.mediaSaved", "Media Saved",
    Description = "Fires when media is saved (created or updated).",
    Group = "Media",
    Icon = "icon-picture",
    RequiredSections = [UmbracoConstants.Applications.Media])]
public sealed class MediaSavedTrigger
    : NotificationTriggerBase<MediaSavedTriggerSettings, MediaSavedTriggerOutput, MediaSavedNotification>,
      INodeScopedTrigger
{
    NodeScopedTriggerTarget? INodeScopedTrigger.GetTargetNode(object output)
        => output is MediaSavedTriggerOutput typed
            ? new NodeScopedTriggerTarget(typed.MediaKey, NodeScopedTriggerTargetKind.Media)
            : null;

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaSavedTrigger"/> class.
    /// </summary>
    public MediaSavedTrigger(TriggerInfrastructure infrastructure) : base(infrastructure)
    {
    }

    /// <inheritdoc />
    public override IEnumerable<TriggerEvent> MapEvent(MediaSavedNotification notification)
    {
        foreach (var media in notification.SavedEntities)
        {
            yield return new TriggerEvent<MediaSavedTriggerOutput>
            {
                TriggerAlias = Alias,
                InitiatorType = TriggerInitiatorType.System,
                // Media saves reuse the same VersionId across edits; UpdateDate advances per
                // save, so include it to avoid deduping legitimate sequential saves.
                IdempotencyKey = GenerateIdempotencyKey(media.Key, media.VersionId, media.UpdateDate),
                Output = new MediaSavedTriggerOutput
                {
                    MediaKey = media.Key,
                    MediaName = media.Name,
                    MediaTypeKey = media.ContentType?.Key,
                    MediaTypeAlias = media.ContentType?.Alias,
                    IsNew = media.CreateDate == media.UpdateDate,
                },
            };
        }
    }

    /// <inheritdoc />
    protected override bool CanHandle(MediaSavedTriggerOutput output, MediaSavedTriggerSettings? settings)
        => EntityTypesFilter.Matches(output.MediaTypeKey, settings?.MediaTypes);
}
