using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Web.Api.Management.Approval.Models;

namespace Umbraco.Automate.Web.Api.Management.Approval.Controllers;

/// <summary>
/// Lists pending approval steps across all automations.
/// </summary>
[ApiVersion("1.0")]
public sealed class PendingApprovalsController : ApprovalControllerBase
{
    private readonly IAutomationRunService _runService;
    private readonly IAutomationService _automationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PendingApprovalsController"/> class.
    /// </summary>
    public PendingApprovalsController(
        IAutomationRunService runService,
        IAutomationService automationService)
    {
        _runService = runService;
        _automationService = automationService;
    }

    /// <summary>
    /// Gets all pending approval steps.
    /// </summary>
    [HttpGet("pending")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(IEnumerable<PendingApprovalResponseModel>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PendingApprovalResponseModel>>> GetPendingApprovals(
        CancellationToken cancellationToken)
    {
        var pendingSteps = await _runService.GetStepRunsByStatusAsync(
            RequestApprovalAction.ApprovalActionAlias,
            StepRunStatus.WaitingForInput,
            cancellationToken);

        var results = new List<PendingApprovalResponseModel>();

        foreach (var (run, stepRun) in pendingSteps)
        {
            var automation = await _automationService.GetAutomationAsync(run.AutomationId, cancellationToken);
            if (automation is null)
            {
                continue;
            }

            results.Add(new PendingApprovalResponseModel
            {
                RunId = run.Id,
                StepId = stepRun.StepId,
                AutomationId = automation.Id,
                AutomationName = automation.Name,
                RequestedUtc = stepRun.StartedUtc,
            });
        }

        return Ok(results);
    }
}
