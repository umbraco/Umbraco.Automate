using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Web.Api.Management.Automation.Models;

namespace Umbraco.Automate.Web.Api.Management.Automation.Controllers;

/// <summary>
/// Updates an existing automation.
/// </summary>
[ApiVersion("1.0")]
public sealed class UpdateAutomationController : AutomationControllerBase
{
    private readonly IAutomationService _automationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateAutomationController"/> class.
    /// </summary>
    public UpdateAutomationController(IAutomationService automationService)
    {
        _automationService = automationService;
    }

    /// <summary>
    /// Updates an existing automation.
    /// </summary>
    [HttpPut("{id:guid}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAutomation(
        Guid id,
        UpdateAutomationRequestModel requestModel,
        CancellationToken cancellationToken = default)
    {
        var existing = await _automationService.GetAutomationAsync(id, cancellationToken);
        if (existing is null)
        {
            return AutomationNotFound();
        }

        existing.Alias = requestModel.Alias;
        existing.Name = requestModel.Name;
        existing.Description = requestModel.Description;
        existing.IsEnabled = requestModel.IsEnabled;
        existing.Trigger = requestModel.Trigger;
        existing.Steps = requestModel.Steps;
        existing.Connections = requestModel.Connections;
        existing.CanvasState = requestModel.CanvasState;

        await _automationService.UpdateAutomationAsync(existing, cancellationToken: cancellationToken);

        return Ok();
    }
}
