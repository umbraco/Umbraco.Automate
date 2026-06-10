using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Security;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Automate.Core.Actions.Middleware;

/// <summary>
/// Middleware that sets the backoffice identity to the workspace's service principal
/// for the duration of action execution. This ensures that any Umbraco CMS code resolving
/// the current user via <c>IBackOfficeSecurityAccessor</c> sees the service account identity.
/// Also enforces the action's <c>RequiredSections</c> against the service account so a
/// post-publish privilege downgrade halts the step rather than running with stale auth.
/// </summary>
internal sealed class BackOfficeIdentityMiddleware : IActionMiddleware
{
    private readonly IUserService _userService;
    private readonly ISectionAccessChecker _sectionAccessChecker;
    private readonly ILogger<BackOfficeIdentityMiddleware> _logger;

    public BackOfficeIdentityMiddleware(
        IUserService userService,
        ISectionAccessChecker sectionAccessChecker,
        ILogger<BackOfficeIdentityMiddleware> logger)
    {
        _userService = userService;
        _sectionAccessChecker = sectionAccessChecker;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ActionResult> ApplyAsync(ActionContext context, ActionMiddlewareDelegate next, CancellationToken cancellationToken)
    {
        if (context.ExecutionContext is null)
        {
            return await next(context, cancellationToken);
        }

        var serviceAccountKey = context.ExecutionContext.ServiceAccountKey;
        var user = await _userService.GetAsync(serviceAccountKey);

        if (user is null)
        {
            _logger.LogError(
                "Service account {ServiceAccountKey} not found for automation {AutomationId} in workspace {WorkspaceId}. "
                + "Ensure the workspace's service account user exists and is not deleted.",
                serviceAccountKey, context.AutomationId, context.ExecutionContext.WorkspaceId);

            return ActionResult.Failed(
                new InvalidOperationException(
                    $"Service account '{serviceAccountKey}' not found. "
                    + "Ensure the workspace's service account user exists and is not deleted."),
                StepRunErrorCategory.Authentication);
        }

        if (context.Action is not null && !_sectionAccessChecker.CanAccess(user, context.Action))
        {
            var required = string.Join(", ", context.Action.RequiredSections);
            _logger.LogWarning(
                "Action {ActionAlias} blocked for automation {AutomationId} — service account {ServiceAccountKey} lacks required sections ({Sections}).",
                context.ActionAlias, context.AutomationId, serviceAccountKey, required);

            return ActionResult.Failed(
                new UnauthorizedAccessException(
                    $"Action '{context.Action.Name}' requires section access ({required}) the service account does not have."),
                StepRunErrorCategory.Authentication);
        }

        var security = new AutomateBackOfficeSecurity(user);
        using var _ = AutomateBackOfficeSecurityAccessor.Set(security);

        return await next(context, cancellationToken);
    }
}
