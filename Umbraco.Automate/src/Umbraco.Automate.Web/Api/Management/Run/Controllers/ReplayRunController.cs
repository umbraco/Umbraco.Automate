using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Runs;
using Umbraco.Cms.Api.Common.Builders;

namespace Umbraco.Automate.Web.Api.Management.Run.Controllers;

/// <summary>
/// Replays a completed or failed run by creating a new run with the same trigger data.
/// </summary>
[ApiVersion("1.0")]
public sealed class ReplayRunController : RunControllerBase
{
    private readonly IAutomationRunService _runService;
    private readonly IAutomationService _automationService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IAutomationExecutor _executor;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReplayRunController"/> class.
    /// </summary>
    public ReplayRunController(
        IAutomationRunService runService,
        IAutomationService automationService,
        IAuthorizationService authorizationService,
        IAutomationExecutor executor)
    {
        _runService = runService;
        _automationService = automationService;
        _authorizationService = authorizationService;
        _executor = executor;
    }

    /// <summary>
    /// Replays a run by creating a new execution with the same trigger data.
    /// The automation must still be published and enabled.
    /// </summary>
    [HttpPost("{id:guid}/replay")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReplayRun(
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

        if (automation.Status != AutomationStatus.Published)
        {
            return Conflict(new ProblemDetailsBuilder()
                .WithTitle("Automation not active")
                .WithDetail("The automation must be published to replay a run.")
                .Build());
        }

        if (run.Status is AutomationRunStatus.Pending or AutomationRunStatus.Running or AutomationRunStatus.Suspended)
        {
            return Conflict(new ProblemDetailsBuilder()
                .WithTitle("Run still in progress")
                .WithDetail("Only completed, failed, or cancelled runs can be replayed.")
                .Build());
        }

        Dictionary<string, object?>? triggerData = null;
        if (!string.IsNullOrEmpty(run.TriggerData))
        {
            triggerData = Core.Dispatch.JsonOptions.DeserializeToUnwrappedDictionary(run.TriggerData);
        }

        await _executor.ExecuteAsync(
            automation,
            "replay",
            run.Id.ToString(),
            triggerData,
            cancellationToken);

        return Accepted();
    }
}
