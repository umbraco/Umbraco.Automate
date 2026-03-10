using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Web.Api.Management.Common.Controllers;
using Umbraco.Automate.Web.Api.Management.Common.Routing;

namespace Umbraco.Automate.Web.Api.Management.Run.Controllers;

/// <summary>
/// Base controller for run endpoints.
/// </summary>
[ApiExplorerSettings(GroupName = Constants.ManagementApi.Feature.Run.GroupName)]
[UmbracoAutomateVersionedManagementApiRoute(Constants.ManagementApi.Feature.Run.RouteSegment)]
public abstract class RunControllerBase : UmbracoAutomateManagementControllerBase;
