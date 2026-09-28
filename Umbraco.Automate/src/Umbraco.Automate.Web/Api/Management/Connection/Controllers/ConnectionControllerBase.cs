using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Connections;
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

    /// <summary>
    /// Returns a 400 Bad Request response for connection settings rejected by an
    /// <see cref="IConnectionSettingsSaveHandler"/>. Built with <see cref="ProblemDetailsBuilder"/> so the
    /// backoffice recognises it and shows the detail on the toast.
    /// </summary>
    private protected IActionResult InvalidConnectionSettings(ConnectionSettingsValidationException exception)
        => BadRequest(new ProblemDetailsBuilder()
            .WithTitle("Invalid connection settings")
            .WithDetail(exception.Message)
            .Build());
}
