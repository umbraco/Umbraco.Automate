using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

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
    private readonly IMemberService _memberService;
    private readonly IMemberGroupService _memberGroupService;
    private readonly ILogger<MemberDeletedTrigger> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemberDeletedTrigger"/> class.
    /// </summary>
    public MemberDeletedTrigger(
        TriggerInfrastructure infrastructure,
        IMemberService memberService,
        IMemberGroupService memberGroupService,
        ILogger<MemberDeletedTrigger> logger)
        : base(infrastructure)
    {
        _memberService = memberService;
        _memberGroupService = memberGroupService;
        _logger = logger;
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
                    // The roles tables are populated until the actual delete commits, so
                    // resolving at notification time still returns the right set. If the
                    // cascade has already cleared the rows by the time this runs, the
                    // filter falls back to "no groups" which the consumer can detect.
                    MemberGroupKeys = MemberGroupResolver.Resolve(_memberService, _memberGroupService, member, _logger),
                },
            };
        }
    }

    /// <inheritdoc />
    protected override bool CanHandle(MemberDeletedTriggerOutput output, MemberDeletedTriggerSettings? settings)
        => EntityTypesFilter.Matches(output.MemberTypeKey, settings?.MemberTypes)
           && GroupKeysFilter.Matches(output.MemberGroupKeys, settings?.MemberGroups);
}
