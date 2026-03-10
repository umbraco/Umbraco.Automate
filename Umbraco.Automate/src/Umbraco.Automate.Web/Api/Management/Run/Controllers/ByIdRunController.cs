using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
    private readonly IAutomationRunService _runService;
    private readonly IUmbracoMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="ByIdRunController"/> class.
    /// </summary>
    public ByIdRunController(IAutomationRunService runService, IUmbracoMapper mapper)
    {
        _runService = runService;
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

        return Ok(_mapper.Map<AutomationRunResponseModel>(run));
    }
}
