using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Web.Api.Management.Common.Controllers;
using Umbraco.Automate.Web.Api.Management.Common.Routing;

namespace Umbraco.Automate.Web.Api.Management.Catalogue.Controllers;

/// <summary>
/// Base controller for catalogue endpoints (triggers and actions registry).
/// </summary>
[ApiExplorerSettings(GroupName = Constants.ManagementApi.Feature.Catalogue.GroupName)]
[UmbracoAutomateVersionedManagementApiRoute(Constants.ManagementApi.Feature.Catalogue.RouteSegment)]
public abstract class CatalogueControllerBase : UmbracoAutomateManagementControllerBase;
