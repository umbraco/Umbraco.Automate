using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Fires when a member is deleted in Umbraco CMS.
/// Produces one <see cref="TriggerEvent"/> per deleted member.
/// </summary>
[Trigger("umbracoAutomate.memberDeleted", "Member Deleted",
    Description = "Fires when a member is deleted.",
    Group = "Members",
    Icon = "icon-delete")]
public sealed class MemberDeletedTrigger
    : NotificationTriggerBase<MemberDeletedTriggerSettings, MemberDeletedTriggerOutput, MemberDeletedNotification>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MemberDeletedTrigger"/> class.
    /// </summary>
    public MemberDeletedTrigger(TriggerInfrastructure infrastructure) : base(infrastructure)
    {
    }

    /// <inheritdoc />
    public override IEnumerable<TriggerEvent> MapEvent(MemberDeletedNotification notification)
    {
        foreach (var member in notification.DeletedEntities)
        {
            yield return new TriggerEvent<MemberDeletedTriggerOutput>
            {
                TriggerAlias = Alias,
                InitiatorType = TriggerInitiatorType.System,
                // Delete is a single terminal transition — VersionId at time of delete
                // identifies the event, and a duplicate notification carries the same id.
                IdempotencyKey = GenerateIdempotencyKey(member.Key, member.VersionId),
                Output = new MemberDeletedTriggerOutput
                {
                    MemberKey = member.Key,
                    MemberName = member.Name,
                    Username = member.Username,
                    Email = member.Email,
                    MemberTypeKey = member.ContentType?.Key,
                    MemberTypeAlias = member.ContentType?.Alias,
                },
            };
        }
    }
}
