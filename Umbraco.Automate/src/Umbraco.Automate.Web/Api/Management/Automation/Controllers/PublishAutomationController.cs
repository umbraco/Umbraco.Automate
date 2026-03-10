using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Automations;

namespace Umbraco.Automate.Web.Api.Management.Automation.Controllers;

/// <summary>
/// Publishes an automation, making its current draft the active version.
/// </summary>
[ApiVersion("1.0")]
public sealed class PublishAutomationController : AutomationControllerBase
{
    private readonly IAutomationService _automationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PublishAutomationController"/> class.
    /// </summary>
    public PublishAutomationController(IAutomationService automationService)
    {
        _automationService = automationService;
    }

    /// <summary>
    /// Publishes an automation.
    /// </summary>
    [HttpPost("{id:guid}/publish")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PublishAutomation(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var existing = await _automationService.GetAutomationAsync(id, cancellationToken);
        if (existing is null)
        {
            return AutomationNotFound();
        }

        await _automationService.PublishAutomationAsync(id, cancellationToken: cancellationToken);

        return Ok();
    }
}
