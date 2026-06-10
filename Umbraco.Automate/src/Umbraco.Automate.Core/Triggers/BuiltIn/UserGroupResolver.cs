using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Resolves the set of user-group keys for an <see cref="Umbraco.Cms.Core.Models.Membership.IUser"/>
/// referenced only by string id (as in <c>UserNotification.AffectedUserId</c>). Used by
/// the user auth triggers to populate the trigger output's <c>UserGroupKeys</c>.
/// </summary>
/// <remarks>
/// Auth notifications don't carry the full <c>IUser</c>, only ids — so we look up the
/// user via <see cref="IUserService.GetAsync(Guid)"/>. The concrete implementation
/// delegates to <c>IBackOfficeUserStore.GetAsync(Guid)</c> which is an indexed lookup,
/// not a full scan. Failures (unparseable id, missing user) degrade to an empty list so
/// the auth flow is never blocked by Automate.
/// </remarks>
internal static class UserGroupResolver
{
    public static IReadOnlyList<Guid> Resolve(IUserService userService, string? affectedUserId, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(affectedUserId))
        {
            return Array.Empty<Guid>();
        }

        if (!Guid.TryParse(affectedUserId, out var userKey))
        {
            // Legacy/int-keyed identities ("-1" SuperUser) end up here — we can't resolve
            // groups for those, so report none. A group filter set to specific GUIDs will
            // naturally fail to match, which is the right outcome.
            return Array.Empty<Guid>();
        }

        try
        {
            var user = userService.GetAsync(userKey).GetAwaiter().GetResult();
            if (user is null || user.Groups is null)
            {
                return Array.Empty<Guid>();
            }

            return user.Groups.Select(g => g.Key).ToArray();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to resolve user groups for user {UserKey}; filter will treat as having no groups",
                userKey);
            return Array.Empty<Guid>();
        }
    }
}
