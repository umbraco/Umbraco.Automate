using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Runs;

namespace Umbraco.Automate.Web.Api.Management.Metrics.Controllers;

/// <summary>
/// Controller for run summary metrics.
/// </summary>
[ApiVersion("1.0")]
public class RunSummaryMetricsController : MetricsControllerBase
{
    private readonly IAutomationRunService _runService;

    /// <summary>
    /// Initializes a new instance of the <see cref="RunSummaryMetricsController"/> class.
    /// </summary>
    public RunSummaryMetricsController(IAutomationRunService runService)
    {
        _runService = runService;
    }

    /// <summary>
    /// Get run summary statistics.
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
        var summary = await _runService.GetRunSummaryAsync(workspaceId, from, to, cancellationToken);
        return Ok(summary);
    }

    /// <summary>
    /// Get run counts grouped by automation.
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
        var counts = await _runService.GetRunCountsByAutomationAsync(workspaceId, from, to, take, cancellationToken);
        return Ok(counts);
    }
}
