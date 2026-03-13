using System.Text.Json;
using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Web.Api.Management.Approval.Models;
using WorkflowCore.Interface;

namespace Umbraco.Automate.Web.Api.Management.Approval.Controllers;

/// <summary>
/// Submits an approval decision for a pending approval step.
/// </summary>
[ApiVersion("1.0")]
public sealed class SubmitApprovalController : ApprovalControllerBase
{
    private readonly IWorkflowHost _workflowHost;
    private readonly ILogger<SubmitApprovalController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubmitApprovalController"/> class.
    /// </summary>
    public SubmitApprovalController(IWorkflowHost workflowHost, ILogger<SubmitApprovalController> logger)
    {
        _workflowHost = workflowHost;
        _logger = logger;
    }

    /// <summary>
    /// Submits an approval decision for a pending step.
    /// </summary>
    /// <param name="runId">The automation run ID.</param>
    /// <param name="stepId">The step ID waiting for approval.</param>
    /// <param name="model">The approval decision.</param>
    [HttpPost("{runId:guid}/steps/{stepId:guid}/decision")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitDecision(
        Guid runId,
        Guid stepId,
        [FromBody] ApprovalDecisionRequestModel model)
    {
        var eventKey = $"{runId}:{stepId}";

        var decision = new ApprovalDecision
        {
            Outcome = model.Outcome,
            Comment = model.Comment,
            ApprovedByUserKey = null, // TODO: extract from authenticated user
            DecisionUtc = DateTime.UtcNow,
        };

        _logger.LogInformation(
            "Publishing approval decision for run {RunId}, step {StepId}: {Outcome}",
            runId, stepId, model.Outcome);

        await _workflowHost.PublishEvent(
            RequestApprovalAction.ApprovalEventName,
            eventKey,
            decision);

        return Ok();
    }
}
