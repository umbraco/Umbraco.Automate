using Umbraco.Cms.Core.Models.Membership;

namespace Umbraco.Automate.Core.Security;

/// <summary>
/// Authorises a user — typically the workspace service account — against CMS content and
/// media nodes. Wraps Umbraco's <c>IContentPermissionService</c> /
/// <c>IMediaPermissionService</c>; the action-pipeline overloads resolve the service-account
/// identity from the ambient backoffice security accessor set by
/// <c>BackOfficeIdentityMiddleware</c>, while the explicit-user overloads are used by
/// dispatch-time authorisation before any action middleware runs.
/// </summary>
public interface IAutomationActionAuthorizer
{
    /// <summary>
    /// Authorises the service account currently set on the ambient backoffice accessor for
    /// the given content node and permission letters.
    /// </summary>
    Task<AutomationAuthorizationResult> AuthorizeContentAsync(
        Guid contentKey,
        IReadOnlySet<string> permissions,
        CancellationToken cancellationToken);

    /// <summary>
    /// Authorises the service account currently set on the ambient backoffice accessor for
    /// the given media node. Media has no per-permission verbs in Umbraco — access is binary
    /// (section + start node).
    /// </summary>
    Task<AutomationAuthorizationResult> AuthorizeMediaAsync(
        Guid mediaKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// Filters a set of content keys to only those the ambient service account is
    /// authorised to access for the given permission letters. Used by search/list actions to
    /// suppress unauthorised results.
    /// </summary>
    Task<IReadOnlySet<Guid>> FilterAuthorizedContentAsync(
        IEnumerable<Guid> contentKeys,
        IReadOnlySet<string> permissions,
        CancellationToken cancellationToken);

    /// <summary>
    /// Authorises an explicit user for the given content node and permissions. Used by the
    /// trigger dispatcher, which resolves the workspace service account before any action
    /// middleware has had a chance to set the ambient accessor.
    /// </summary>
    Task<AutomationAuthorizationResult> AuthorizeContentAsync(
        IUser user,
        Guid contentKey,
        IReadOnlySet<string> permissions,
        CancellationToken cancellationToken);

    /// <summary>
    /// Authorises an explicit user for the given media node.
    /// </summary>
    Task<AutomationAuthorizationResult> AuthorizeMediaAsync(
        IUser user,
        Guid mediaKey,
        CancellationToken cancellationToken);
}
