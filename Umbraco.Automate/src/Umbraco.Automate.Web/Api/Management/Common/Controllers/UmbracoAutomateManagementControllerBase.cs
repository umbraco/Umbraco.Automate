using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Web.Authorization;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Api.Common.Builders;
using Umbraco.Cms.Api.Common.Filters;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Security.Authorization;
using Umbraco.Extensions;

namespace Umbraco.Automate.Web.Api.Management.Common.Controllers;

/// <summary>
/// Base controller for Umbraco Automate Management API controllers.
/// </summary>
[ApiController]
[MapToApi(Constants.ManagementApi.ApiName)]
[JsonOptionsName(Constants.ManagementApi.ApiName)]
[Authorize(Policy = AutomateAuthorizationPolicies.SectionAccessAutomate)]
[Produces("application/json")]
public abstract class UmbracoAutomateManagementControllerBase : ControllerBase
{
    /// <summary>
    /// Creates an operation status result using a problem details builder.
    /// </summary>
    protected static IActionResult OperationStatusResult<TEnum>(
        TEnum status,
        Func<ProblemDetailsBuilder, IActionResult> builder)
        where TEnum : Enum
        => builder(new ProblemDetailsBuilder().WithOperationStatus(status));

    /// <summary>
    /// Returns a 404 Not Found response for an automation.
    /// </summary>
    protected IActionResult AutomationNotFound()
        => NotFound(new ProblemDetailsBuilder()
            .WithTitle("Automation not found")
            .WithDetail("The specified automation could not be found.")
            .Build());

    /// <summary>
    /// Returns a 404 Not Found response for a run.
    /// </summary>
    protected IActionResult RunNotFound()
        => NotFound(new ProblemDetailsBuilder()
            .WithTitle("Run not found")
            .WithDetail("The specified run could not be found.")
            .Build());

    /// <summary>
    /// Returns a 403 Forbidden response.
    /// </summary>
    /// <remarks>
    /// Use this method instead of the controller base class's <c>Forbid()</c> method.
    /// This ensures a proper 403 status code is returned to the client.
    /// </remarks>
    protected IActionResult Forbidden()
        => new StatusCodeResult(StatusCodes.Status403Forbidden);

    /// <summary>
    /// Gets the key of the current back-office user.
    /// </summary>
    protected static Guid CurrentUserKey(IBackOfficeSecurityAccessor backOfficeSecurityAccessor)
        => backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser?.Key
           ?? throw new InvalidOperationException("No backoffice user found");

    /// <summary>
    /// Requires the current user to be an administrator. Returns <c>null</c> if the user is an admin,
    /// a 401 Unauthorized result if no user is found, or a 403 Forbidden result otherwise.
    /// </summary>
    /// <remarks>
    /// This checks backoffice user identity only. Service account (UserKind.Api) callers
    /// do not have a backoffice session and will receive 401. If service account access
    /// to admin endpoints is needed in the future, this method must be extended.
    /// </remarks>
    protected IActionResult? RequireAdmin(IBackOfficeSecurityAccessor accessor)
    {
        var user = accessor.BackOfficeSecurity?.CurrentUser;
        if (user is null)
        {
            return new UnauthorizedResult();
        }

        return user.IsAdmin() ? null : Forbidden();
    }

    /// <summary>
    /// Returns a 409 Conflict response for an optimistic concurrency violation.
    /// </summary>
    protected IActionResult ConcurrencyConflict(string entityName)
        => Conflict(new ProblemDetails
        {
            Title = "Concurrency conflict",
            Detail = $"The {entityName} was modified by another request. Reload and try again.",
            Status = StatusCodes.Status409Conflict,
        });

    /// <summary>
    /// Authorizes workspace access for the current user. Returns <c>null</c> if authorized,
    /// or a 403 Forbidden result if the user is not a member of the workspace.
    /// </summary>
    protected async Task<IActionResult?> AuthorizeWorkspaceAccessAsync(
        IAuthorizationService authorizationService,
        Guid workspaceId)
    {
        var result = await authorizationService.AuthorizeResourceAsync(
            User,
            WorkspaceAccessResource.WithId(workspaceId),
            AutomateAuthorizationPolicies.WorkspaceAccess);

        return result.Succeeded ? null : Forbidden();
    }
}
