using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Web.Api.Management.Common.Controllers;
using Umbraco.Automate.Web.Api.Management.Common.Routing;
using Umbraco.Cms.Api.Common.Builders;

namespace Umbraco.Automate.Web.Api.Management.Workspace.Group.Controllers;

/// <summary>
/// Base controller for workspace group endpoints, nested under workspaces.
/// Route: /workspaces/{workspaceId}/groups/...
/// </summary>
[ApiExplorerSettings(GroupName = Constants.ManagementApi.Feature.Workspace.GroupName)]
[UmbracoAutomateVersionedManagementApiRoute(Constants.ManagementApi.Feature.Group.RouteSegment)]
public abstract class WorkspaceGroupControllerBase : UmbracoAutomateManagementControllerBase
{
    /// <summary>
    /// Returns a 404 Not Found response for a workspace group.
    /// </summary>
    protected IActionResult GroupNotFound()
        => NotFound(new ProblemDetailsBuilder()
            .WithTitle("Group not found")
            .WithDetail("The specified workspace group could not be found.")
            .Build());

    /// <summary>
    /// Authorizes workspace access and returns a 403 if denied.
    /// </summary>
    protected Task<IActionResult?> AuthorizeWorkspaceAsync(
        IAuthorizationService authorizationService,
        Guid workspaceId)
        => AuthorizeWorkspaceAccessAsync(authorizationService, workspaceId);
}
