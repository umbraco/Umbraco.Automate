using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Web.Api.Management.Automation.Models;
using Umbraco.Cms.Api.Common.ViewModels.Pagination;
using Umbraco.Cms.Core.Mapping;

namespace Umbraco.Automate.Web.Api.Management.Automation.Controllers;

/// <summary>
/// Gets all automations with optional paging and filtering.
/// </summary>
[ApiVersion("1.0")]
public sealed class AllAutomationController : AutomationControllerBase
{
    private readonly IAutomationService _automationService;
    private readonly IUmbracoMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="AllAutomationController"/> class.
    /// </summary>
    public AllAutomationController(IAutomationService automationService, IUmbracoMapper mapper)
    {
        _automationService = automationService;
        _mapper = mapper;
    }

    /// <summary>
    /// Gets a paged list of automations.
    /// </summary>
    [HttpGet]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(PagedViewModel<AutomationItemResponseModel>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedViewModel<AutomationItemResponseModel>>> GetAllAutomations(
        string? filter = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        var (items, total) = await _automationService.GetAutomationsPagedAsync(filter, skip, take, cancellationToken);

        return Ok(new PagedViewModel<AutomationItemResponseModel>
        {
            Total = total,
            Items = _mapper.MapEnumerable<Core.Automations.Automation, AutomationItemResponseModel>(items),
        });
    }
}
