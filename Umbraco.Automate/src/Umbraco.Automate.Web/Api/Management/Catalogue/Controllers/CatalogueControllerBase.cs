using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Web.Api.Management.Common.Controllers;
using Umbraco.Automate.Web.Api.Management.Common.Routing;
using Umbraco.Cms.Api.Common.Builders;

namespace Umbraco.Automate.Web.Api.Management.Catalogue.Controllers;

/// <summary>
/// Base controller for catalogue endpoints (triggers and actions registry).
/// </summary>
[ApiExplorerSettings(GroupName = Constants.ManagementApi.Feature.Catalogue.GroupName)]
[UmbracoAutomateVersionedManagementApiRoute(Constants.ManagementApi.Feature.Catalogue.RouteSegment)]
public abstract class CatalogueControllerBase : UmbracoAutomateManagementControllerBase
{
    /// <summary>
    /// Returns a 400 Bad Request response for step settings that the settings resolver rejected.
    /// Built with <see cref="ProblemDetailsBuilder"/> so the backoffice recognises it and shows
    /// the detail on the toast.
    /// </summary>
    /// <param name="exception">The exception thrown by settings resolution.</param>
    protected IActionResult InvalidSettings(SettingsResolutionException exception)
        => BadRequest(new ProblemDetailsBuilder()
            .WithTitle("Invalid settings")
            .WithDetail(exception.Message)
            .Build());
}
