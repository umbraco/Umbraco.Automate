using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Web.Api.Management.Common.Controllers;
using Umbraco.Automate.Web.Api.Management.Common.Routing;
using Umbraco.Cms.Api.Common.Builders;

namespace Umbraco.Automate.Web.Api.Management.Versioning.Controllers;

/// <summary>
/// Base controller for Version History management API endpoints.
/// </summary>
[ApiExplorerSettings(GroupName = Constants.ManagementApi.Feature.Versioning.GroupName)]
[UmbracoAutomateVersionedManagementApiRoute(Constants.ManagementApi.Feature.Versioning.RouteSegment)]
public abstract class VersioningControllerBase : UmbracoAutomateManagementControllerBase
{
    /// <summary>
    /// Returns a 404 Not Found response for a version.
    /// </summary>
    protected IActionResult VersionNotFound(int version)
        => NotFound(new ProblemDetailsBuilder()
            .WithTitle("Version not found")
            .WithDetail($"Version {version} was not found.")
            .Build());

    /// <summary>
    /// Returns a 400 Bad Request response for an unknown entity type.
    /// </summary>
    protected IActionResult UnknownEntityType(string entityType)
        => BadRequest(new ProblemDetailsBuilder()
            .WithTitle("Unknown entity type")
            .WithDetail($"Entity type '{entityType}' is not supported.")
            .Build());

    /// <summary>
    /// Returns a 404 Not Found response for an entity.
    /// </summary>
    protected IActionResult EntityNotFound(string entityType, Guid entityId)
        => NotFound(new ProblemDetailsBuilder()
            .WithTitle("Entity not found")
            .WithDetail($"Entity of type '{entityType}' with ID '{entityId}' was not found.")
            .Build());
}
