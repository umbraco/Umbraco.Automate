using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Web.Api.Management.Automation.Models;

namespace Umbraco.Automate.Web.Api.Management.Automation.Controllers;

/// <summary>
/// Creates a new automation.
/// </summary>
[ApiVersion("1.0")]
public sealed class CreateAutomationController : AutomationControllerBase
{
    private readonly IAutomationService _automationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateAutomationController"/> class.
    /// </summary>
    public CreateAutomationController(IAutomationService automationService)
    {
        _automationService = automationService;
    }

    /// <summary>
    /// Creates a new automation.
    /// </summary>
    [HttpPost]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateAutomation(
        CreateAutomationRequestModel requestModel,
        CancellationToken cancellationToken = default)
    {
        var automation = new Core.Automations.Automation
        {
            Alias = requestModel.Alias,
            Name = requestModel.Name,
            Description = requestModel.Description,
            IsEnabled = requestModel.IsEnabled,
            Trigger = requestModel.Trigger,
            Steps = requestModel.Steps,
            Connections = requestModel.Connections,
            CanvasState = requestModel.CanvasState,
        };

        var created = await _automationService.CreateAutomationAsync(automation, cancellationToken: cancellationToken);

        return CreatedAtAction(
            nameof(ByIdAutomationController.GetAutomationById),
            nameof(ByIdAutomationController).Replace("Controller", string.Empty),
            new { id = created.Id },
            created.Id.ToString());
    }
}
