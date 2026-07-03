using System.Text.Json;
using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Web.Api.Management.Approval.Models;
using Umbraco.Cms.Core.Security.Authorization;
using Umbraco.Extensions;

namespace Umbraco.Automate.Web.Api.Management.Approval.Controllers;

/// <summary>
/// Lists pending approval steps in workspaces the current user can access.
/// </summary>
[ApiVersion("1.0")]
public sealed class PendingApprovalsController : ApprovalControllerBase
{
    private readonly IAutomationRunService _runService;
    private readonly IAutomationService _automationService;
    private readonly IWorkspaceService _workspaceService;
    private readonly IAuthorizationHelper _authorizationHelper;

    /// <summary>
    /// Initializes a new instance of the <see cref="PendingApprovalsController"/> class.
    /// </summary>
    public PendingApprovalsController(
        IAutomationRunService runService,
        IAutomationService automationService,
        IWorkspaceService workspaceService,
        IAuthorizationHelper authorizationHelper)
    {
        _runService = runService;
        _automationService = automationService;
        _workspaceService = workspaceService;
        _authorizationHelper = authorizationHelper;
    }

    /// <summary>
    /// Gets pending approval steps. Results are scoped to workspaces the current user has access to.
    /// </summary>
    [HttpGet("pending")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(IEnumerable<PendingApprovalResponseModel>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PendingApprovalResponseModel>>> GetPendingApprovals(
        CancellationToken cancellationToken)
    {
        // Admins see all pending approvals; non-admins see only those in their workspaces.
        IReadOnlySet<Guid>? accessibleWorkspaceIds = null;

        var user = _authorizationHelper.GetUmbracoUser(User);
        if (!user.IsAdmin())
        {
            var userGroupKeys = user.Groups.Select(g => g.Key).ToList();
            accessibleWorkspaceIds = await _workspaceService.GetAccessibleWorkspaceIdsAsync(userGroupKeys, cancellationToken);
        }

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

            // Filter out approvals in workspaces the user cannot access.
            if (accessibleWorkspaceIds is not null && !accessibleWorkspaceIds.Contains(automation.WorkspaceId))
            {
                continue;
            }

            results.Add(new PendingApprovalResponseModel
            {
                RunId = run.Id,
                StepId = stepRun.StepId,
                AutomationId = automation.Id,
                AutomationName = automation.Name,
                Prompt = ReadPrompt(stepRun.OutputData),
                RequestedUtc = stepRun.StartedUtc,
            });
        }

        return Ok(results);
    }

    /// <summary>
    /// Extracts the approval prompt from the step's serialised output data, if present.
    /// </summary>
    private static string? ReadPrompt(string? outputData)
    {
        if (string.IsNullOrWhiteSpace(outputData))
        {
            return null;
        }

        try
        {
            var output = JsonSerializer.Deserialize<ApprovalRequestOutput>(
                outputData, Umbraco.Automate.Core.Dispatch.JsonOptions.Default);
            return output?.Prompt;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
