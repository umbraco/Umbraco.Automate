using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Web.Api.Management.Run.Models;

namespace Umbraco.Automate.Web.Api.Management.Run.Controllers;

/// <summary>
/// Gets the recorded trigger data of a run. A separate, on-demand endpoint rather than a field on
/// the run detail response: trigger payloads (webhook bodies, say) can be large, and the run
/// detail is loaded — and reloaded after every lifecycle action — whether or not the user ever
/// looks at the trigger data. Masking and truncation happen in
/// <see cref="IAutomationRunService"/>, as for the step run data endpoint.
/// </summary>
[ApiVersion("1.0")]
public sealed class TriggerDataRunController : RunControllerBase
{
    private readonly IAutomationService _automationService;
    private readonly IAutomationRunService _runService;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TriggerDataRunController"/> class.
    /// </summary>
    public TriggerDataRunController(
        IAutomationService automationService,
        IAutomationRunService runService,
        IAuthorizationService authorizationService)
    {
        _automationService = automationService;
        _runService = runService;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Gets a run's trigger data as pretty-printed JSON, with sensitive values masked and the
    /// value truncated to a maximum length.
    /// </summary>
    [HttpGet("{id:guid}/trigger-data")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(RunTriggerDataResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTriggerData(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var data = await _runService.GetTriggerDataAsync(id, cancellationToken);
        if (data is null)
        {
            return RunNotFound();
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

        // The service returns the trigger data already masked and truncated for display.
        return Ok(new RunTriggerDataResponseModel
        {
            TriggerData = data.TriggerData,
            TriggerDataTruncated = data.TriggerDataTruncated,
        });
    }
}
