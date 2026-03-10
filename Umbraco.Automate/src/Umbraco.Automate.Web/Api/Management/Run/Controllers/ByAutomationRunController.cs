using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Web.Api.Management.Run.Models;
using Umbraco.Cms.Api.Common.ViewModels.Pagination;

namespace Umbraco.Automate.Web.Api.Management.Run.Controllers;

/// <summary>
/// Gets runs for a specific automation.
/// </summary>
[ApiVersion("1.0")]
public sealed class ByAutomationRunController : RunControllerBase
{
    private readonly IAutomationRunService _runService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ByAutomationRunController"/> class.
    /// </summary>
    public ByAutomationRunController(IAutomationRunService runService)
    {
        _runService = runService;
    }

    /// <summary>
    /// Gets paged runs for a specific automation.
    /// </summary>
    [HttpGet("by-automation/{automationId:guid}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(PagedViewModel<AutomationRunResponseModel>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedViewModel<AutomationRunResponseModel>>> GetRunsByAutomation(
        Guid automationId,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        var (items, total) = await _runService.GetRunsByAutomationPagedAsync(automationId, skip, take, cancellationToken);

        var responseItems = items.Select(MapToResponse).ToList();

        return Ok(new PagedViewModel<AutomationRunResponseModel>
        {
            Total = total,
            Items = responseItems,
        });
    }

    private static AutomationRunResponseModel MapToResponse(AutomationRun run)
        => new()
        {
            Id = run.Id,
            AutomationId = run.AutomationId,
            AutomationVersion = run.AutomationVersion,
            Status = run.Status,
            StartedUtc = run.StartedUtc,
            CompletedUtc = run.CompletedUtc,
            InitiatedBy = run.InitiatedBy,
            CorrelationId = run.CorrelationId,
            Error = run.Error,
            StepRuns = run.StepRuns.Select(MapStepRun).ToList(),
        };

    private static StepRunResponseModel MapStepRun(StepRun stepRun)
        => new()
        {
            Id = stepRun.Id,
            StepId = stepRun.StepId,
            ActionAlias = stepRun.ActionAlias,
            Status = stepRun.Status,
            StartedUtc = stepRun.StartedUtc,
            CompletedUtc = stepRun.CompletedUtc,
            Error = stepRun.Error,
            RetryCount = stepRun.RetryCount,
            DurationMs = stepRun.Duration?.TotalMilliseconds,
        };
}
