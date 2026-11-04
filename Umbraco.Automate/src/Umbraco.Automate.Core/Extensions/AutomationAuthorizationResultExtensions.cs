using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Security;

namespace Umbraco.Automate.Extensions;

/// <summary>
/// Helpers for actions that check a service account's access and need to tell a missing item apart
/// from a denied one, for example to take a not-found exit instead of failing the step.
/// </summary>
public static class AutomationAuthorizationResultExtensions
{
    /// <summary>
    /// Converts a denied authorisation result into the failed <see cref="ActionResult"/> that
    /// the <c>*OrFailAsync</c> helpers return: an <see cref="UnauthorizedAccessException"/>
    /// carrying the failure reason, categorised as <see cref="StepRunErrorCategory.Authentication"/>.
    /// </summary>
    public static ActionResult ToFailedActionResult(this AutomationAuthorizationResult result)
        => ActionResult.Failed(
            new UnauthorizedAccessException(result.FailureReason),
            StepRunErrorCategory.Authentication);

    /// <summary>
    /// Authorises the service account against <paramref name="contentKey"/> for the given
    /// permission letters, without converting the outcome to an <see cref="ActionResult"/>, so
    /// the caller can inspect <see cref="AutomationAuthorizationResult.IsNotFound"/>.
    /// </summary>
    public static Task<AutomationAuthorizationResult> AuthorizeContentAsync(
        this IAutomationActionAuthorizer authorizer,
        Guid contentKey,
        IReadOnlyList<string> permissions,
        CancellationToken cancellationToken)
        => authorizer.AuthorizeContentAsync(contentKey, AutomationActionAuthorizerExtensions.ToSet(permissions), cancellationToken);
}
