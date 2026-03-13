using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Web.Api.Management.Common.Controllers;
using Umbraco.Automate.Web.Api.Management.Common.Routing;
using Umbraco.Cms.Api.Common.Builders;

namespace Umbraco.Automate.Web.Api.Management.Group.Controllers;

/// <summary>
/// Base controller for automation group endpoints.
/// </summary>
[ApiExplorerSettings(GroupName = Constants.ManagementApi.Feature.Group.GroupName)]
[UmbracoAutomateVersionedManagementApiRoute(Constants.ManagementApi.Feature.Group.RouteSegment)]
public abstract class AutomationGroupControllerBase : UmbracoAutomateManagementControllerBase
{
    /// <summary>
    /// Returns a 404 Not Found response for an automation group.
    /// </summary>
    protected IActionResult GroupNotFound()
        => NotFound(new ProblemDetailsBuilder()
            .WithTitle("Group not found")
            .WithDetail("The specified automation group could not be found.")
            .Build());
}
