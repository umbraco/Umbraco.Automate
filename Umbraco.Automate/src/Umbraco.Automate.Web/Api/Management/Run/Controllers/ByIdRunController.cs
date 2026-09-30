using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Web.Api.Management.Run.Models;
using Umbraco.Cms.Core.Mapping;

namespace Umbraco.Automate.Web.Api.Management.Run.Controllers;

/// <summary>
/// Gets a single run by ID with full step run details.
/// </summary>
[ApiVersion("1.0")]
public sealed class ByIdRunController : RunControllerBase
{
    private readonly IAutomationService _automationService;
    private readonly IAutomationRunService _runService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUmbracoMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="ByIdRunController"/> class.
    /// </summary>
    public ByIdRunController(
        IAutomationService automationService,
        IAutomationRunService runService,
        IAuthorizationService authorizationService,
        IUmbracoMapper mapper)
    {
        _automationService = automationService;
        _runService = runService;
        _authorizationService = authorizationService;
        _mapper = mapper;
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

        var automation = await _automationService.GetAutomationAsync(run.AutomationId, cancellationToken);
        if (automation is null)
        {
            return RunNotFound();
        }

        var forbidden = await AuthorizeWorkspaceAccessAsync(_authorizationService, automation.WorkspaceId);
        if (forbidden is not null)
        {
            return forbidden;
        }

        // The trigger may have changed since this run; fall back to the current one if the version
        // snapshot is gone (e.g. cleaned up by version retention).
        var definition = await _automationService.GetAutomationVersionSnapshotAsync(run.AutomationId, run.AutomationVersion, cancellationToken)
            ?? automation;

        var model = _mapper.Map<AutomationRunResponseModel>(run)!;
        model.TriggerAlias = definition.Trigger?.TriggerAlias;

        return Ok(model);
    }
}
