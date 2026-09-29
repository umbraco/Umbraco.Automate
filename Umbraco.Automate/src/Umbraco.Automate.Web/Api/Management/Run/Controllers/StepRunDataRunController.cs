using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Web.Api.Management.Run.Models;
using Umbraco.Cms.Api.Common.Builders;

namespace Umbraco.Automate.Web.Api.Management.Run.Controllers;

/// <summary>
/// Gets the recorded input and output of a single step run, loaded on demand when the step is
/// expanded in the run view rather than with the run itself, since the payloads can be large.
/// </summary>
[ApiVersion("1.0")]
public sealed class StepRunDataRunController : RunControllerBase
{
    private readonly IAutomationService _automationService;
    private readonly IAutomationRunService _runService;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="StepRunDataRunController"/> class.
    /// </summary>
    public StepRunDataRunController(
        IAutomationService automationService,
        IAutomationRunService runService,
        IAuthorizationService authorizationService)
    {
        _automationService = automationService;
        _runService = runService;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Gets a step run's input and output as pretty-printed JSON, with sensitive values masked and
    /// each value truncated to a maximum length.
    /// </summary>
    [HttpGet("{id:guid}/step-runs/{stepRunId:guid}/data")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(StepRunDataResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStepRunData(
        Guid id,
        Guid stepRunId,
        CancellationToken cancellationToken = default)
    {
        var data = await _runService.GetStepRunDataAsync(id, stepRunId, cancellationToken);
        if (data is null)
        {
            return StepRunNotFound();
        }

        var automation = await _automationService.GetAutomationAsync(data.AutomationId, cancellationToken);
        if (automation is null)
        {
            return RunNotFound();
        }

        var forbidden = await AuthorizeWorkspaceAccessAsync(_authorizationService, automation.WorkspaceId);
        if (forbidden is not null)
        {
            return forbidden;
        }

        // The service returns the payloads already masked and truncated for display.
        return Ok(new StepRunDataResponseModel
        {
            Input = data.Input,
            InputTruncated = data.InputTruncated,
            Output = data.Output,
            OutputTruncated = data.OutputTruncated,
        });
    }

    private IActionResult StepRunNotFound()
        => NotFound(new ProblemDetailsBuilder()
            .WithTitle("Step run not found")
            .WithDetail("The specified step run could not be found in this run.")
            .Build());
}
