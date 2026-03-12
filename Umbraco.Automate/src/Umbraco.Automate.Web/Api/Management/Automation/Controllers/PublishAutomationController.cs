using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
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
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PublishAutomationController"/> class.
    /// </summary>
    public PublishAutomationController(
        IAutomationService automationService,
        IAuthorizationService authorizationService)
    {
        _automationService = automationService;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Publishes an automation.
    /// </summary>
    [HttpPost("{id:guid}/publish")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> PublishAutomation(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var existing = await _automationService.GetAutomationAsync(id, cancellationToken);
        if (existing is null)
        {
            return AutomationNotFound();
        }

        var forbidden = await AuthorizeWorkspaceAccessAsync(_authorizationService, existing.WorkspaceId);
        if (forbidden is not null)
        {
            return forbidden;
        }

        try
        {
            await _automationService.PublishAutomationAsync(id, cancellationToken: cancellationToken);
        }
        catch (AutomationValidationException ex)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Title = "Validation failed",
                Detail = ex.Message,
                Status = StatusCodes.Status422UnprocessableEntity,
                Extensions = { ["errors"] = ex.Errors },
            });
        }

        return Ok();
    }
}
