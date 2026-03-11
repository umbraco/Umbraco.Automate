using Microsoft.AspNetCore.Mvc;
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
}
