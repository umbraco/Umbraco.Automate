using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Web.Api.Management.Automation.Models;

namespace Umbraco.Automate.Web.Api.Management.Automation.Controllers;

/// <summary>
/// Gets a single automation by ID.
/// </summary>
[ApiVersion("1.0")]
public sealed class ByIdAutomationController : AutomationControllerBase
{
    private readonly IAutomationService _automationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ByIdAutomationController"/> class.
    /// </summary>
    public ByIdAutomationController(IAutomationService automationService)
    {
        _automationService = automationService;
    }

    /// <summary>
    /// Gets an automation by its unique ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(AutomationResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAutomationById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var automation = await _automationService.GetAutomationAsync(id, cancellationToken);
        if (automation is null)
        {
            return AutomationNotFound();
        }

        return Ok(MapToResponse(automation));
    }

    private static AutomationResponseModel MapToResponse(Core.Automations.Automation automation)
        => new()
        {
            Id = automation.Id,
            Alias = automation.Alias,
            Name = automation.Name,
            Description = automation.Description,
            IsEnabled = automation.IsEnabled,
            Status = automation.Status,
            PublishedVersion = automation.PublishedVersion,
            DraftVersion = automation.DraftVersion,
            Trigger = automation.Trigger,
            Steps = automation.Steps,
            Connections = automation.Connections,
            CanvasState = automation.CanvasState,
            Version = automation.Version,
            DateCreated = automation.DateCreated,
            DateModified = automation.DateModified,
        };
}
