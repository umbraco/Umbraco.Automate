using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Fires when a member is saved in Umbraco CMS (covers both new registrations and
/// subsequent edits — Umbraco raises <see cref="MemberSavedNotification"/> for both,
/// with <see cref="MemberSavedTriggerOutput.IsNew"/> distinguishing them).
/// Produces one <see cref="TriggerEvent"/> per saved member.
/// </summary>
[Trigger("umbracoAutomate.memberSaved", "Member Saved",
    Description = "Fires when a member is saved (created or updated).",
    Group = "Members",
    Icon = "icon-user")]
public sealed class MemberSavedTrigger
    : NotificationTriggerBase<MemberSavedTriggerSettings, MemberSavedTriggerOutput, MemberSavedNotification>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MemberSavedTrigger"/> class.
    /// </summary>
    public MemberSavedTrigger(TriggerInfrastructure infrastructure) : base(infrastructure)
    {
    }

    /// <inheritdoc />
    public override IEnumerable<TriggerEvent> MapEvent(MemberSavedNotification notification)
    {
        foreach (var member in notification.SavedEntities)
        {
            yield return new TriggerEvent<MemberSavedTriggerOutput>
            {
                TriggerAlias = Alias,
                InitiatorType = TriggerInitiatorType.System,
                // Member saves reuse the same VersionId across edits; UpdateDate advances per
                // save, so include it to avoid deduping legitimate sequential saves.
                IdempotencyKey = GenerateIdempotencyKey(member.Key, member.VersionId, member.UpdateDate),
                Output = new MemberSavedTriggerOutput
                {
                    MemberKey = member.Key,
                    MemberName = member.Name,
                    Username = member.Username,
                    Email = member.Email,
                    MemberTypeKey = member.ContentType?.Key,
                    MemberTypeAlias = member.ContentType?.Alias,
                    IsNew = member.CreateDate == member.UpdateDate,
                },
            };
        }
    }
}
