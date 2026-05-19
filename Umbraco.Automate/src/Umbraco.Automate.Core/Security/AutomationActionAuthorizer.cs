using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.AuthorizationStatus;

namespace Umbraco.Automate.Core.Security;

/// <inheritdoc />
internal sealed class AutomationActionAuthorizer : IAutomationActionAuthorizer
{
    private readonly IContentPermissionService _contentPermissionService;
    private readonly IMediaPermissionService _mediaPermissionService;
    private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;
    private readonly ILogger<AutomationActionAuthorizer> _logger;

    public AutomationActionAuthorizer(
        IContentPermissionService contentPermissionService,
        IMediaPermissionService mediaPermissionService,
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
        ILogger<AutomationActionAuthorizer> logger)
    {
        _contentPermissionService = contentPermissionService;
        _mediaPermissionService = mediaPermissionService;
        _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AutomationAuthorizationResult> AuthorizeContentAsync(
        Guid contentKey,
        IReadOnlySet<string> permissions,
        CancellationToken cancellationToken)
    {
        var user = _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser;
        if (user is null)
        {
            return AutomationAuthorizationResult.Fail(
                "No backoffice identity available. Ensure the automation is running within a workspace with a valid service account.");
        }

        // CMS expects ISet<string>; copy into a HashSet so callers can pass any IReadOnlySet variant.
        var permissionSet = permissions as ISet<string> ?? new HashSet<string>(permissions, StringComparer.Ordinal);

        var status = permissionSet.Count > 0
            ? await _contentPermissionService.AuthorizeAccessAsync(user, [contentKey], permissionSet)
            : await _contentPermissionService.AuthorizeAccessAsync(user, [contentKey], new HashSet<string>());

        if (status == ContentAuthorizationStatus.Success)
        {
            return AutomationAuthorizationResult.Success;
        }

        var reason = MapContentReason(status, contentKey, permissionSet);
        _logger.LogDebug(
            "Content authorisation denied for service account {UserKey} on node {ContentKey} (permissions [{Permissions}]): {Status}",
            user.Key, contentKey, string.Join(", ", permissionSet), status);

        return AutomationAuthorizationResult.Fail(reason);
    }

    /// <inheritdoc />
    public async Task<AutomationAuthorizationResult> AuthorizeMediaAsync(
        Guid mediaKey,
        CancellationToken cancellationToken)
    {
        var user = _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser;
        if (user is null)
        {
            return AutomationAuthorizationResult.Fail(
                "No backoffice identity available. Ensure the automation is running within a workspace with a valid service account.");
        }

        var status = await _mediaPermissionService.AuthorizeAccessAsync(user, [mediaKey]);

        if (status == MediaAuthorizationStatus.Success)
        {
            return AutomationAuthorizationResult.Success;
        }

        var reason = MapMediaReason(status, mediaKey);
        _logger.LogDebug(
            "Media authorisation denied for service account {UserKey} on node {MediaKey}: {Status}",
            user.Key, mediaKey, status);

        return AutomationAuthorizationResult.Fail(reason);
    }

    /// <inheritdoc />
    public async Task<IReadOnlySet<Guid>> FilterAuthorizedContentAsync(
        IEnumerable<Guid> contentKeys,
        IReadOnlySet<string> permissions,
        CancellationToken cancellationToken)
    {
        var keys = contentKeys as IReadOnlyCollection<Guid> ?? contentKeys.ToList();
        if (keys.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var user = _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser;
        if (user is null)
        {
            // Without an identity we cannot authorise anything — fail closed and return empty.
            return new HashSet<Guid>();
        }

        var permissionSet = permissions as ISet<string> ?? new HashSet<string>(permissions, StringComparer.Ordinal);
        var authorized = new HashSet<Guid>();

        // CMS's permission service authorises a *batch* of keys atomically — a single failure
        // returns failure for the whole call. We need per-key results, so loop. Sequential is
        // acceptable; result sets are bounded by the calling action's Limit (default ~50).
        foreach (var key in keys)
        {
            var status = permissionSet.Count > 0
                ? await _contentPermissionService.AuthorizeAccessAsync(user, [key], permissionSet)
                : await _contentPermissionService.AuthorizeAccessAsync(user, [key], new HashSet<string>());

            if (status == ContentAuthorizationStatus.Success)
            {
                authorized.Add(key);
            }
        }

        return authorized;
    }

    private static string MapContentReason(ContentAuthorizationStatus status, Guid contentKey, ISet<string> permissions)
        => status switch
        {
            ContentAuthorizationStatus.NotFound =>
                $"Content node '{contentKey}' not found.",
            ContentAuthorizationStatus.UnauthorizedMissingPathAccess =>
                $"Service account is not allowed to access node '{contentKey}' — outside its start-node path.",
            ContentAuthorizationStatus.UnauthorizedMissingPermissionAccess =>
                $"Service account lacks the required permissions ({string.Join(", ", permissions)}) on node '{contentKey}'.",
            ContentAuthorizationStatus.UnauthorizedMissingDescendantAccess =>
                $"Service account is not allowed to access descendants of node '{contentKey}'.",
            ContentAuthorizationStatus.UnauthorizedMissingRootAccess =>
                "Service account is not allowed to access the content root.",
            ContentAuthorizationStatus.UnauthorizedMissingBinAccess =>
                "Service account is not allowed to access the recycle bin.",
            ContentAuthorizationStatus.UnauthorizedMissingCulture =>
                $"Service account is not allowed to access the cultures required for node '{contentKey}'.",
            _ => $"Service account is not authorised to access content node '{contentKey}'.",
        };

    private static string MapMediaReason(MediaAuthorizationStatus status, Guid mediaKey)
        => status switch
        {
            MediaAuthorizationStatus.NotFound =>
                $"Media node '{mediaKey}' not found.",
            MediaAuthorizationStatus.UnauthorizedMissingPathAccess =>
                $"Service account is not allowed to access media node '{mediaKey}' — outside its start-node path.",
            MediaAuthorizationStatus.UnauthorizedMissingRootAccess =>
                "Service account is not allowed to access the media root.",
            MediaAuthorizationStatus.UnauthorizedMissingBinAccess =>
                "Service account is not allowed to access the media recycle bin.",
            _ => $"Service account is not authorised to access media node '{mediaKey}'.",
        };
}
