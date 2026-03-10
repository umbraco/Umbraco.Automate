using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Automations;

namespace Umbraco.Automate.Web.Api.Management.Automation.Controllers;

/// <summary>
/// Deletes an automation.
/// </summary>
[ApiVersion("1.0")]
public sealed class DeleteAutomationController : AutomationControllerBase
{
    private readonly IAutomationService _automationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteAutomationController"/> class.
    /// </summary>
    public DeleteAutomationController(IAutomationService automationService)
    {
        _automationService = automationService;
    }

    /// <summary>
    /// Deletes an automation and all its runs.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAutomation(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var deleted = await _automationService.DeleteAutomationAsync(id, cancellationToken);
        if (!deleted)
        {
            return AutomationNotFound();
        }

        return Ok();
    }
}
