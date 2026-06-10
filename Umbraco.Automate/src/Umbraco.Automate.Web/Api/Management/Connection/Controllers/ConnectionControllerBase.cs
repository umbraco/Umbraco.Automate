using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Web.Api.Management.Common.Controllers;
using Umbraco.Automate.Web.Api.Management.Common.Routing;
using Umbraco.Cms.Api.Common.Builders;

namespace Umbraco.Automate.Web.Api.Management.Connection.Controllers;

/// <summary>
/// Base controller for connection endpoints.
/// </summary>
[ApiExplorerSettings(GroupName = Constants.ManagementApi.Feature.Connection.GroupName)]
[UmbracoAutomateVersionedManagementApiRoute(Constants.ManagementApi.Feature.Connection.RouteSegment)]
public abstract class ConnectionControllerBase : UmbracoAutomateManagementControllerBase
{
    /// <summary>
    /// Returns a 404 Not Found response for a connection.
    /// </summary>
    protected IActionResult ConnectionNotFound()
        => NotFound(new ProblemDetailsBuilder()
            .WithTitle("Connection not found")
            .WithDetail("The specified connection could not be found.")
            .Build());
}
