using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Web.Api.Management.Approval.Models;
using Umbraco.Cms.Core.Security;
using WorkflowCore.Interface;

namespace Umbraco.Automate.Web.Api.Management.Approval.Controllers;

/// <summary>
/// Submits an approval decision for a pending approval step.
/// </summary>
[ApiVersion("1.0")]
public sealed class SubmitApprovalController : ApprovalControllerBase
{
    private readonly IWorkflowHost _workflowHost;
    private readonly IAutomationRunService _runService;
    private readonly IAutomationService _automationService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;
    private readonly ILogger<SubmitApprovalController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubmitApprovalController"/> class.
    /// </summary>
    public SubmitApprovalController(
        IWorkflowHost workflowHost,
        IAutomationRunService runService,
        IAutomationService automationService,
        IAuthorizationService authorizationService,
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
        ILogger<SubmitApprovalController> logger)
    {
        _workflowHost = workflowHost;
        _runService = runService;
        _automationService = automationService;
        _authorizationService = authorizationService;
        _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
        _logger = logger;
    }

    /// <summary>
    /// Submits an approval decision for a pending step.
    /// </summary>
    /// <param name="runId">The automation run ID.</param>
    /// <param name="stepId">The step ID waiting for approval.</param>
    /// <param name="model">The approval decision.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    [HttpPost("{runId:guid}/steps/{stepId:guid}/decision")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SubmitDecision(
        Guid runId,
        Guid stepId,
        [FromBody] ApprovalDecisionRequestModel model,
        CancellationToken cancellationToken = default)
    {
        // Resolve the run and its owning automation so we can enforce workspace membership.
        var run = await _runService.GetRunAsync(runId, cancellationToken);
        if (run is null)
        {
            return RunNotFound();
        }

        var automation = await _automationService.GetAutomationAsync(run.AutomationId, cancellationToken);
        if (automation is null)
        {
            return RunNotFound();
        }

        // Only members of the automation's workspace may approve or reject.
        var forbidden = await AuthorizeWorkspaceAccessAsync(_authorizationService, automation.WorkspaceId);
        if (forbidden is not null)
        {
            return forbidden;
        }

        var eventKey = $"{runId}:{stepId}";

        var decision = new ApprovalDecision
        {
            Outcome = model.Outcome,
            Comment = model.Comment,
            ApprovedByUserKey = _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser?.Key,
            DecisionUtc = DateTime.UtcNow,
        };

        _logger.LogInformation(
            "Publishing approval decision for run {RunId}, step {StepId} by user {UserKey}: {Outcome}",
            runId, stepId, decision.ApprovedByUserKey, model.Outcome);

        await _workflowHost.PublishEvent(
            RequestApprovalAction.ApprovalEventName,
            eventKey,
            decision);

        return Ok();
    }
}
