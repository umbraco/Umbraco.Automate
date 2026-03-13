using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Web.Api.Management.Common.Controllers;
using Umbraco.Automate.Web.Api.Management.Common.Routing;

namespace Umbraco.Automate.Web.Api.Management.Metrics.Controllers;

/// <summary>
/// Base controller for metrics endpoints.
/// </summary>
[ApiExplorerSettings(GroupName = Constants.ManagementApi.Feature.Metrics.GroupName)]
[UmbracoAutomateVersionedManagementApiRoute(Constants.ManagementApi.Feature.Metrics.RouteSegment)]
public abstract class MetricsControllerBase : UmbracoAutomateManagementControllerBase;
