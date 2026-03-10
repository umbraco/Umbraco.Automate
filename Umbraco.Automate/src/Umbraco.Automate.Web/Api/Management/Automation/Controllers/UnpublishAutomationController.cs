using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Automations;

namespace Umbraco.Automate.Web.Api.Management.Automation.Controllers;

/// <summary>
/// Unpublishes an automation, setting it to inactive.
/// </summary>
[ApiVersion("1.0")]
public sealed class UnpublishAutomationController : AutomationControllerBase
{
    private readonly IAutomationService _automationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnpublishAutomationController"/> class.
    /// </summary>
    public UnpublishAutomationController(IAutomationService automationService)
    {
        _automationService = automationService;
    }

    /// <summary>
    /// Unpublishes an automation.
    /// </summary>
    [HttpPost("{id:guid}/unpublish")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnpublishAutomation(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var existing = await _automationService.GetAutomationAsync(id, cancellationToken);
        if (existing is null)
        {
            return AutomationNotFound();
        }

        await _automationService.UnpublishAutomationAsync(id, cancellationToken: cancellationToken);

        return Ok();
    }
}
