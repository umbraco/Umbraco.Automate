using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Web.Api.Management.Common.Controllers;
using Umbraco.Automate.Web.Api.Management.Common.Routing;

namespace Umbraco.Automate.Web.Api.Management.Automation.Controllers;

/// <summary>
/// Base controller for automation endpoints.
/// </summary>
[ApiExplorerSettings(GroupName = Constants.ManagementApi.Feature.Automation.GroupName)]
[UmbracoAutomateVersionedManagementApiRoute(Constants.ManagementApi.Feature.Automation.RouteSegment)]
public abstract class AutomationControllerBase : UmbracoAutomateManagementControllerBase;
