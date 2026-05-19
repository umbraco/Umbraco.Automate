using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

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
    private readonly IMemberService _memberService;
    private readonly IMemberGroupService _memberGroupService;
    private readonly ILogger<MemberSavedTrigger> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemberSavedTrigger"/> class.
    /// </summary>
    public MemberSavedTrigger(
        TriggerInfrastructure infrastructure,
        IMemberService memberService,
        IMemberGroupService memberGroupService,
        ILogger<MemberSavedTrigger> logger)
        : base(infrastructure)
    {
        _memberService = memberService;
        _memberGroupService = memberGroupService;
        _logger = logger;
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
                    MemberGroupKeys = MemberGroupResolver.Resolve(_memberService, _memberGroupService, member, _logger),
                    IsNew = member.CreateDate == member.UpdateDate,
                },
            };
        }
    }

    /// <inheritdoc />
    protected override bool CanHandle(MemberSavedTriggerOutput output, MemberSavedTriggerSettings? settings)
        => EntityTypesFilter.Matches(output.MemberTypeKey, settings?.MemberTypes)
           && GroupKeysFilter.Matches(output.MemberGroupKeys, settings?.MemberGroups);
}
