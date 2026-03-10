using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Web.Api.Management.Run.Models;

namespace Umbraco.Automate.Web.Api.Management.Run.Controllers;

/// <summary>
/// Gets a single run by ID with full step run details.
/// </summary>
[ApiVersion("1.0")]
public sealed class ByIdRunController : RunControllerBase
{
    private readonly IAutomationRunService _runService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ByIdRunController"/> class.
    /// </summary>
    public ByIdRunController(IAutomationRunService runService)
    {
        _runService = runService;
    }

    /// <summary>
    /// Gets a run by its unique ID, including all step runs.
    /// </summary>
    [HttpGet("{id:guid}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(AutomationRunResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRunById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var run = await _runService.GetRunAsync(id, cancellationToken);
        if (run is null)
        {
            return RunNotFound();
        }

        var response = new AutomationRunResponseModel
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
            StepRuns = run.StepRuns.Select(s => new StepRunResponseModel
            {
                Id = s.Id,
                StepId = s.StepId,
                ActionAlias = s.ActionAlias,
                Status = s.Status,
                StartedUtc = s.StartedUtc,
                CompletedUtc = s.CompletedUtc,
                Error = s.Error,
                RetryCount = s.RetryCount,
                DurationMs = s.Duration?.TotalMilliseconds,
            }).ToList(),
        };

        return Ok(response);
    }
}
