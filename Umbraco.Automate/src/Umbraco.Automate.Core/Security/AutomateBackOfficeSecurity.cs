using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;

namespace Umbraco.Automate.Core.Security;

/// <summary>
/// <see cref="IBackOfficeSecurity"/> implementation for automation execution contexts.
/// Wraps a resolved <see cref="IUser"/> (the workspace's service account) so that any
/// Umbraco CMS code that accesses the current backoffice user sees the service principal.
/// </summary>
internal sealed class AutomateBackOfficeSecurity : IBackOfficeSecurity
{
    /// <summary>
    /// Initializes a new instance backed by the given service account user.
    /// </summary>
    public AutomateBackOfficeSecurity(IUser user) => CurrentUser = user;

    /// <inheritdoc />
    public IUser? CurrentUser { get; }

    /// <inheritdoc />
    /// <remarks>Always returns <c>true</c> — the service account is the authenticated identity for the run.</remarks>
    public bool IsAuthenticated() => true;

    /// <inheritdoc />
    public bool UserHasSectionAccess(string section, IUser user)
        => user.AllowedSections.Contains(section);
}
