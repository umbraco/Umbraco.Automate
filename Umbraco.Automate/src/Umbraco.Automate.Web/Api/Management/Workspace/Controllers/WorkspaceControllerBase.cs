using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Web.Api.Management.Common.Controllers;
using Umbraco.Automate.Web.Api.Management.Common.Routing;
using Umbraco.Cms.Api.Common.Builders;

namespace Umbraco.Automate.Web.Api.Management.Workspace.Controllers;

/// <summary>
/// Base controller for workspace endpoints.
/// </summary>
[ApiExplorerSettings(GroupName = Constants.ManagementApi.Feature.Workspace.GroupName)]
[UmbracoAutomateVersionedManagementApiRoute(Constants.ManagementApi.Feature.Workspace.RouteSegment)]
public abstract class WorkspaceControllerBase : UmbracoAutomateManagementControllerBase
{
    /// <summary>
    /// Returns a 404 Not Found response for a workspace.
    /// </summary>
    protected IActionResult WorkspaceNotFound()
        => NotFound(new ProblemDetailsBuilder()
            .WithTitle("Workspace not found")
            .WithDetail("The specified workspace could not be found.")
            .Build());

    /// <summary>
    /// Returns a 400 Bad Request response for a service account that is not an API user. Built with
    /// <see cref="ProblemDetailsBuilder"/> so the backoffice recognises it and shows the detail on the toast.
    /// </summary>
    private protected IActionResult InvalidServiceAccount(WorkspaceServiceAccountValidationException exception)
        => BadRequest(new ProblemDetailsBuilder()
            .WithTitle("Invalid service account")
            .WithDetail(exception.Message)
            .Build());
}
