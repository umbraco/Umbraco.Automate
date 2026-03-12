using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Api.Common.Builders;
using Umbraco.Cms.Api.Common.Filters;
using Umbraco.Automate.Web.Authorization;

namespace Umbraco.Automate.Web.Api.Management.Common.Controllers;

/// <summary>
/// Base controller for Umbraco Automate Management API controllers.
/// </summary>
[ApiController]
[MapToApi(Constants.ManagementApi.ApiName)]
[JsonOptionsName(Constants.ManagementApi.ApiName)]
[Authorize(Policy = AutomateAuthorizationPolicies.SectionAccessAutomate)]
[Produces("application/json")]
public abstract class UmbracoAutomateManagementControllerBase : ControllerBase
{
    /// <summary>
    /// Creates an operation status result using a problem details builder.
    /// </summary>
    protected static IActionResult OperationStatusResult<TEnum>(
        TEnum status,
        Func<ProblemDetailsBuilder, IActionResult> builder)
        where TEnum : Enum
        => builder(new ProblemDetailsBuilder().WithOperationStatus(status));

    /// <summary>
    /// Returns a 404 Not Found response for an automation.
    /// </summary>
    protected IActionResult AutomationNotFound()
        => NotFound(new ProblemDetailsBuilder()
            .WithTitle("Automation not found")
            .WithDetail("The specified automation could not be found.")
            .Build());

    /// <summary>
    /// Returns a 404 Not Found response for a run.
    /// </summary>
    protected IActionResult RunNotFound()
        => NotFound(new ProblemDetailsBuilder()
            .WithTitle("Run not found")
            .WithDetail("The specified run could not be found.")
            .Build());
}
