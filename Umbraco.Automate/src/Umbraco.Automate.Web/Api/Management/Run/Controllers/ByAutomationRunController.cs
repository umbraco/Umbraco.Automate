using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Web.Api.Management.Run.Models;
using Umbraco.Cms.Api.Common.ViewModels.Pagination;
using Umbraco.Cms.Core.Mapping;

namespace Umbraco.Automate.Web.Api.Management.Run.Controllers;

/// <summary>
/// Gets runs for a specific automation.
/// </summary>
[ApiVersion("1.0")]
public sealed class ByAutomationRunController : RunControllerBase
{
    private readonly IAutomationRunService _runService;
    private readonly IUmbracoMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="ByAutomationRunController"/> class.
    /// </summary>
    public ByAutomationRunController(IAutomationRunService runService, IUmbracoMapper mapper)
    {
        _runService = runService;
        _mapper = mapper;
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

        return Ok(new PagedViewModel<AutomationRunResponseModel>
        {
            Total = total,
            Items = _mapper.MapEnumerable<AutomationRun, AutomationRunResponseModel>(items),
        });
    }
}
