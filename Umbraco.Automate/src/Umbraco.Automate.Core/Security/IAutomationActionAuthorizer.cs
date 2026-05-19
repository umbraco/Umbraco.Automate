namespace Umbraco.Automate.Core.Security;

/// <summary>
/// Authorises the current automation's service account against CMS content and media nodes.
/// Wraps Umbraco's <c>IContentPermissionService</c> / <c>IMediaPermissionService</c> and resolves
/// the service-account identity from the ambient backoffice security accessor set by
/// <c>BackOfficeIdentityMiddleware</c>.
/// </summary>
public interface IAutomationActionAuthorizer
{
    /// <summary>
    /// Authorises the service account for the given content node and permission letters.
    /// </summary>
    Task<AutomationAuthorizationResult> AuthorizeContentAsync(
        Guid contentKey,
        IReadOnlySet<string> permissions,
        CancellationToken cancellationToken);

    /// <summary>
    /// Authorises the service account for the given media node. Media has no per-permission verbs
    /// in Umbraco — access is binary (section + start node).
    /// </summary>
    Task<AutomationAuthorizationResult> AuthorizeMediaAsync(
        Guid mediaKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// Filters a set of content keys to only those the service account is authorised to access
    /// for the given permission letters. Used by search/list actions to suppress unauthorised results.
    /// </summary>
    Task<IReadOnlySet<Guid>> FilterAuthorizedContentAsync(
        IEnumerable<Guid> contentKeys,
        IReadOnlySet<string> permissions,
        CancellationToken cancellationToken);
}
