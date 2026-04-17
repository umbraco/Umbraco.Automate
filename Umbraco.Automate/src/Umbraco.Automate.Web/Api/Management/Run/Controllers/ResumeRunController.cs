using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Runs;
using Umbraco.Cms.Api.Common.Builders;

namespace Umbraco.Automate.Web.Api.Management.Run.Controllers;

/// <summary>
/// Resumes a suspended workflow, returning it to the running state.
/// </summary>
[ApiVersion("1.0")]
public sealed class ResumeRunController : RunControllerBase
{
    private readonly IAutomationRunService _runService;
    private readonly IAutomationService _automationService;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResumeRunController"/> class.
    /// </summary>
    public ResumeRunController(
        IAutomationRunService runService,
        IAutomationService automationService,
        IAuthorizationService authorizationService)
    {
        _runService = runService;
        _automationService = automationService;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Resumes a suspended workflow. Only runs currently in <c>Suspended</c> state can be resumed.
    /// </summary>
    [HttpPost("{id:guid}/resume")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ResumeRun(
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

        var result = await _runService.ResumeRunAsync(id, cancellationToken);
        return result switch
        {
            RunLifecycleResult.Success => Accepted(),
            RunLifecycleResult.NotFound => RunNotFound(),
            RunLifecycleResult.InvalidState => Conflict(new ProblemDetailsBuilder()
                .WithTitle("Run cannot be resumed")
                .WithDetail("Only suspended runs can be resumed.")
                .Build()),
            RunLifecycleResult.NoWorkflowInstance => Conflict(new ProblemDetailsBuilder()
                .WithTitle("Run missing workflow instance")
                .WithDetail("This run was created before lifecycle tracking was enabled and cannot be resumed.")
                .Build()),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }
}
