using Umbraco.Automate.Core.Actions;

namespace Umbraco.Automate.Core.Security;

/// <summary>
/// Convenience helpers for the common "check then fail-the-step" content/media authorisation
/// preludes that built-in actions use.
/// </summary>
public static class AutomationActionAuthorizerExtensions
{
    /// <summary>
    /// Authorises the service account against <paramref name="contentKey"/> for the given
    /// permission letters and returns a failed <see cref="ActionResult"/> when access is
    /// denied. Returns <c>null</c> on success so the caller can short-circuit with
    /// <c>if (await ... is { } failure) return failure;</c>.
    /// </summary>
    public static async Task<ActionResult?> AuthorizeContentOrFailAsync(
        this IAutomationActionAuthorizer authorizer,
        Guid contentKey,
        IReadOnlyList<string> permissions,
        CancellationToken cancellationToken)
    {
        var result = await authorizer.AuthorizeContentAsync(contentKey, ToSet(permissions), cancellationToken);
        return result.Authorized
            ? null
            : ActionResult.Failed(
                new UnauthorizedAccessException(result.FailureReason),
                StepRunErrorCategory.Authentication);
    }

    /// <summary>
    /// Authorises the service account against <paramref name="mediaKey"/> and returns a
    /// failed <see cref="ActionResult"/> when access is denied. Returns <c>null</c> on
    /// success.
    /// </summary>
    public static async Task<ActionResult?> AuthorizeMediaOrFailAsync(
        this IAutomationActionAuthorizer authorizer,
        Guid mediaKey,
        CancellationToken cancellationToken)
    {
        var result = await authorizer.AuthorizeMediaAsync(mediaKey, cancellationToken);
        return result.Authorized
            ? null
            : ActionResult.Failed(
                new UnauthorizedAccessException(result.FailureReason),
                StepRunErrorCategory.Authentication);
    }

    /// <summary>
    /// Authorises the service account against a target parent content node (or the content
    /// root when <paramref name="parentKey"/> is <c>null</c>) for the given permission letters,
    /// and returns a failed <see cref="ActionResult"/> when access is denied. Returns
    /// <c>null</c> on success. Used alongside <see cref="AuthorizeContentOrFailAsync"/> by
    /// actions that relocate content, so both the source and the destination are checked.
    /// </summary>
    public static async Task<ActionResult?> AuthorizeContentParentOrFailAsync(
        this IAutomationActionAuthorizer authorizer,
        Guid? parentKey,
        IReadOnlyList<string> permissions,
        CancellationToken cancellationToken)
    {
        var result = await authorizer.AuthorizeContentParentAsync(parentKey, ToSet(permissions), cancellationToken);
        return result.Authorized
            ? null
            : ActionResult.Failed(
                new UnauthorizedAccessException(result.FailureReason),
                StepRunErrorCategory.Authentication);
    }

    /// <summary>
    /// Authorises the service account against a target parent media node (or the media root
    /// when <paramref name="parentKey"/> is <c>null</c>) and returns a failed
    /// <see cref="ActionResult"/> when access is denied. Returns <c>null</c> on success. Used
    /// alongside <see cref="AuthorizeMediaOrFailAsync"/> by actions that relocate media.
    /// </summary>
    public static async Task<ActionResult?> AuthorizeMediaParentOrFailAsync(
        this IAutomationActionAuthorizer authorizer,
        Guid? parentKey,
        CancellationToken cancellationToken)
    {
        var result = await authorizer.AuthorizeMediaParentAsync(parentKey, cancellationToken);
        return result.Authorized
            ? null
            : ActionResult.Failed(
                new UnauthorizedAccessException(result.FailureReason),
                StepRunErrorCategory.Authentication);
    }

    private static IReadOnlySet<string> ToSet(IReadOnlyList<string> permissions)
        => permissions.Count == 0
            ? EmptyPermissionSet
            : new HashSet<string>(permissions, StringComparer.Ordinal);

    private static readonly IReadOnlySet<string> EmptyPermissionSet = new HashSet<string>();
}
