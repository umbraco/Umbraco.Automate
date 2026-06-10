using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Web.Api.Management.Common.Controllers;
using Umbraco.Automate.Web.Api.Management.Common.Routing;

namespace Umbraco.Automate.Web.Api.Management.Approval.Controllers;

/// <summary>
/// Base controller for approval endpoints.
/// </summary>
[ApiExplorerSettings(GroupName = Constants.ManagementApi.Feature.Approval.GroupName)]
[UmbracoAutomateVersionedManagementApiRoute(Constants.ManagementApi.Feature.Approval.RouteSegment)]
public abstract class ApprovalControllerBase : UmbracoAutomateManagementControllerBase;
