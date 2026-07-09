using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Cms.Core.Security.Authorization;
using Umbraco.Extensions;

namespace Umbraco.Automate.Web.Api.Management.Metrics.Controllers;

/// <summary>
/// Controller for run summary metrics. Results are scoped to workspaces the current user has access to.
/// </summary>
[ApiVersion("1.0")]
public class RunSummaryMetricsController : MetricsControllerBase
{
    private readonly IAutomationRunService _runService;
    private readonly IWorkspaceService _workspaceService;
    private readonly IAuthorizationHelper _authorizationHelper;

    /// <summary>
    /// Initializes a new instance of the <see cref="RunSummaryMetricsController"/> class.
    /// </summary>
    public RunSummaryMetricsController(
        IAutomationRunService runService,
        IWorkspaceService workspaceService,
        IAuthorizationHelper authorizationHelper)
    {
        _runService = runService;
        _workspaceService = workspaceService;
        _authorizationHelper = authorizationHelper;
    }

    /// <summary>
    /// Get run summary statistics. Scoped to workspaces the current user has access to.
    /// </summary>
    [HttpGet]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(RunSummary), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRunSummary(
        [FromQuery] Guid? workspaceId = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var workspaceIds = await ResolveAccessibleWorkspacesAsync(workspaceId, cancellationToken);
        var summary = await _runService.GetRunSummaryAsync(workspaceIds, from, to, cancellationToken);
        return Ok(summary);
    }

    /// <summary>
    /// Get run counts grouped by automation. Scoped to workspaces the current user has access to.
    /// </summary>
    [HttpGet("by-automation")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(IEnumerable<AutomationRunCount>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRunCountsByAutomation(
        [FromQuery] Guid? workspaceId = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int take = 10,
        CancellationToken cancellationToken = default)
    {
        var workspaceIds = await ResolveAccessibleWorkspacesAsync(workspaceId, cancellationToken);
        var counts = await _runService.GetRunCountsByAutomationAsync(workspaceIds, from, to, take, cancellationToken);
        return Ok(counts);
    }

    /// <summary>
    /// Resolves the set of workspaces to scope metrics to. Admins are unscoped (<c>null</c> = all);
    /// non-admins are limited to their accessible workspaces. An explicit <paramref name="workspaceId"/>
    /// filter is intersected with the accessible set.
    /// </summary>
    private async Task<IReadOnlySet<Guid>?> ResolveAccessibleWorkspacesAsync(
        Guid? workspaceId,
        CancellationToken cancellationToken)
    {
        IReadOnlySet<Guid>? workspaceIds = null;

        var user = _authorizationHelper.GetUmbracoUser(User);
        if (!user.IsAdmin())
        {
            var userGroupKeys = user.Groups.Select(g => g.Key).ToList();
            workspaceIds = await _workspaceService.GetAccessibleWorkspaceIdsAsync(userGroupKeys, cancellationToken);
        }

        if (workspaceId.HasValue)
        {
            var requested = new HashSet<Guid> { workspaceId.Value };
            workspaceIds = workspaceIds is not null
                ? (IReadOnlySet<Guid>)requested.Intersect(workspaceIds).ToHashSet()
                : requested;
        }

        return workspaceIds;
    }
}
