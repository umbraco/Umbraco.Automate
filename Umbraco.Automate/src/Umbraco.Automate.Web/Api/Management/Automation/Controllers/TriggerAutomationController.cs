using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Web.Api.Management.Automation.Models;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="TriggerAutomationController"/> class.
    /// </summary>
    public TriggerAutomationController(
        IAutomationService automationService,
        IAuthorizationService authorizationService,
        IAutomationExecutor executor,
        ICircuitBreakerService circuitBreaker,
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor)
    {
        _automationService = automationService;
        _authorizationService = authorizationService;
        _executor = executor;
        _circuitBreaker = circuitBreaker;
        _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
    }

    /// <summary>
    /// Manually triggers an automation. The automation must be published and enabled.
    /// </summary>
    [HttpPost("{id:guid}/trigger")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> TriggerAutomation(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] TriggerAutomationRequestModel? request = null,
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

        // Optional stand-in payload for the trigger the automation would normally wait on —
        // lets a webhook automation be developed and tested without an external caller.
        // Unwrapped to plain types so bindings can traverse it and it survives the
        // Newtonsoft round-trip in the WorkflowCore persistence layer.
        var triggerOutputData = request?.TriggerOutputData is { Count: > 0 } raw
            ? Core.Dispatch.JsonOptions.UnwrapDictionary(raw)
            : null;

        await _executor.ExecuteAsync(
            automation,
            TriggerInitiatorType.User,
            initiatorId: userKey?.ToString(),
            triggerOutputData,
            cancellationToken);

        return Accepted();
    }
}
