using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Resolves the set of member-group keys for an <see cref="IMember"/>. Used by member
/// triggers to populate the trigger output's <c>MemberGroupKeys</c> so that downstream
/// filtering and binding can branch on group membership.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IMember"/> deliberately omits a Groups property (see the commented-out
/// <c>IEnumerable&lt;object&gt; Groups</c> on <see cref="Umbraco.Cms.Core.Models.Membership.IMembershipUser"/>),
/// so resolution requires two service calls per saved member:
/// </para>
/// <list type="number">
///   <item><see cref="IMemberService.GetAllRolesIds(int)"/> for the member's role ids</item>
///   <item><see cref="IMemberGroupService.GetByIdsAsync"/> to map ids to <c>IMemberGroup.Key</c></item>
/// </list>
/// <para>
/// We call the async API synchronously here because the surrounding notification handler is
/// already off any ASP.NET sync context, and <see cref="INotificationTrigger{TNotification}.MapEvent"/>
/// is itself synchronous. Resolution failures degrade gracefully to an empty list — the
/// group filter then treats the member as belonging to no groups.
/// </para>
/// </remarks>
internal static class MemberGroupResolver
{
    public static IReadOnlyList<Guid> Resolve(
        IMemberService memberService,
        IMemberGroupService memberGroupService,
        IMember member,
        ILogger logger)
    {
        try
        {
            var roleIds = memberService.GetAllRolesIds(member.Id).ToArray();
            if (roleIds.Length == 0)
            {
                return Array.Empty<Guid>();
            }

            var groups = memberGroupService.GetByIdsAsync(roleIds).GetAwaiter().GetResult();
            return groups.Select(g => g.Key).ToArray();
        }
        catch (Exception ex)
        {
            // Log and treat as "no groups" rather than failing the whole notification —
            // a single bad lookup must not block the CMS save flow.
            logger.LogWarning(ex,
                "Failed to resolve member groups for member {MemberKey}; filter will treat as having no groups",
                member.Key);
            return Array.Empty<Guid>();
        }
    }
}
