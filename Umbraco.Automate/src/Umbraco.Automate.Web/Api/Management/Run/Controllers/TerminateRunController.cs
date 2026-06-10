using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Runs;
using Umbraco.Cms.Api.Common.Builders;

namespace Umbraco.Automate.Web.Api.Management.Run.Controllers;

/// <summary>
/// Terminates a running or suspended workflow.
/// </summary>
[ApiVersion("1.0")]
public sealed class TerminateRunController : RunControllerBase
{
    private readonly IAutomationRunService _runService;
    private readonly IAutomationService _automationService;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TerminateRunController"/> class.
    /// </summary>
    public TerminateRunController(
        IAutomationRunService runService,
        IAutomationService automationService,
        IAuthorizationService authorizationService)
    {
        _runService = runService;
        _automationService = automationService;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Terminates a running or suspended workflow. The run is recorded as <c>Cancelled</c>.
    /// </summary>
    [HttpPost("{id:guid}/terminate")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> TerminateRun(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var run = await _runService.GetRunAsync(id, cancellationToken);
        if (run is null)
        {
            return RunNotFound();
        }

        var automation = await _automationService.GetAutomationAsync(run.AutomationId, cancellationToken);
        if (automation is null)
        {
            return AutomationNotFound();
        }

        var forbidden = await AuthorizeWorkspaceAccessAsync(_authorizationService, automation.WorkspaceId);
        if (forbidden is not null)
        {
            return forbidden;
        }

        var result = await _runService.TerminateRunAsync(id, cancellationToken);
        return result switch
        {
            RunLifecycleResult.Success => Accepted(),
            RunLifecycleResult.NotFound => RunNotFound(),
            RunLifecycleResult.InvalidState => Conflict(new ProblemDetailsBuilder()
                .WithTitle("Run cannot be terminated")
                .WithDetail("Only running, suspended, or pending runs can be terminated.")
                .Build()),
            RunLifecycleResult.NoWorkflowInstance => Conflict(new ProblemDetailsBuilder()
                .WithTitle("Run missing workflow instance")
                .WithDetail("This run was created before lifecycle tracking was enabled and cannot be terminated.")
                .Build()),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }
}
