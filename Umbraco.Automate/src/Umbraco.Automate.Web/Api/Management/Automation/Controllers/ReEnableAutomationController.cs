using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Automations;

namespace Umbraco.Automate.Web.Api.Management.Automation.Controllers;

/// <summary>
/// Re-enables an automation that was auto-disabled by the circuit breaker, resetting its health
/// back to Healthy. The publish status is unaffected.
/// </summary>
[ApiVersion("1.0")]
public sealed class ReEnableAutomationController : AutomationControllerBase
{
    private readonly IAutomationService _automationService;
    private readonly IAuthorizationService _authorizationService;
    private readonly ICircuitBreakerService _circuitBreaker;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReEnableAutomationController"/> class.
    /// </summary>
    public ReEnableAutomationController(
        IAutomationService automationService,
        IAuthorizationService authorizationService,
        ICircuitBreakerService circuitBreaker)
    {
        _automationService = automationService;
        _authorizationService = authorizationService;
        _circuitBreaker = circuitBreaker;
    }

    /// <summary>
    /// Re-enables an auto-disabled automation, clearing its circuit-breaker health.
    /// </summary>
    [HttpPost("{id:guid}/re-enable")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReEnableAutomation(
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

        await _circuitBreaker.ResetAsync(id, cancellationToken);

        return Ok();
    }
}
