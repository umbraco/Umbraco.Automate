using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Cms.Api.Common.Builders;
using Umbraco.Cms.Core.Security;

namespace Umbraco.Automate.Web.Api.Management.Automation.Controllers;

/// <summary>
/// Manually triggers an automation, starting an immediate run.
/// </summary>
[ApiVersion("1.0")]
public sealed class TriggerAutomationController : AutomationControllerBase
{
    private readonly IAutomationService _automationService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IAutomationExecutor _executor;
    private readonly ICircuitBreakerService _circuitBreaker;
    private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;
    private readonly TriggerCollection _triggers;

    /// <summary>
    /// Initializes a new instance of the <see cref="TriggerAutomationController"/> class.
    /// </summary>
    public TriggerAutomationController(
        IAutomationService automationService,
        IAuthorizationService authorizationService,
        IAutomationExecutor executor,
        ICircuitBreakerService circuitBreaker,
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
        TriggerCollection triggers)
    {
        _automationService = automationService;
        _authorizationService = authorizationService;
        _executor = executor;
        _circuitBreaker = circuitBreaker;
        _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
        _triggers = triggers;
    }

    /// <summary>
    /// Manually triggers an automation. The automation must be published and enabled.
    /// </summary>
    [HttpPost("{id:guid}/trigger")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> TriggerAutomation(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var automation = await _automationService.GetAutomationAsync(id, cancellationToken);
        if (automation is null)
        {
            return AutomationNotFound();
        }

        var forbidden = await AuthorizeWorkspaceAccessAsync(_authorizationService, automation.WorkspaceId);
        if (forbidden is not null)
        {
            return forbidden;
        }

        if (automation.Status != AutomationStatus.Published)
        {
            return Conflict(new ProblemDetailsBuilder()
                .WithTitle("Automation not active")
                .WithDetail("The automation must be published to be triggered.")
                .Build());
        }

        if (!await _circuitBreaker.IsRunAllowedAsync(id, TriggerInitiatorType.User, cancellationToken))
        {
            return Conflict(new ProblemDetailsBuilder()
                .WithTitle("Automation auto-disabled")
                .WithDetail("This automation has been auto-disabled by the circuit breaker. Re-enable it to run.")
                .Build());
        }

        // Start the run directly via the executor rather than dispatching a trigger event.
        // A trigger event would fan out to every published automation that subscribes to
        // the same alias — but "Run now" is imperative: this specific automation, now.
        // Matches the pattern used by ReplayRunController.
        var userKey = _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser?.Key;

        // Triggers that can be run on demand supply their own stand-in for the payload they
        // would normally wait on — the webhook trigger builds one from its saved test request,
        // so an automation can be exercised without an external caller. Triggers that need no
        // payload return nothing, and ones that never opted in are simply started bare.
        Dictionary<string, object?>? triggerOutputData = null;
        var trigger = automation.Trigger is not null ? _triggers.GetByAlias(automation.Trigger.TriggerAlias) : null;
        if (trigger is ISupportsManualRun runnableTrigger)
        {
            var settings = automation.Trigger?.Settings is { Count: > 0 } rawSettings
                ? trigger.ResolveSettings(rawSettings)
                : null;

            var manualRunOutput = runnableTrigger.CreateManualRunOutput(settings);
            if (!manualRunOutput.Success)
            {
                return BadRequest(new ProblemDetailsBuilder()
                    .WithTitle("Trigger settings invalid")
                    .WithDetail(manualRunOutput.Error!)
                    .Build());
            }

            triggerOutputData = manualRunOutput.Data;
        }

        await _executor.ExecuteAsync(
            automation,
            TriggerInitiatorType.User,
            initiatorId: userKey?.ToString(),
            triggerOutputData,
            cancellationToken);

        return Accepted();
    }
}
