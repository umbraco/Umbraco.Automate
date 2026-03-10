using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Web.Api.Management.Automation.Models;
using Umbraco.Cms.Core.Mapping;

namespace Umbraco.Automate.Web.Api.Management.Automation.Controllers;

/// <summary>
/// Gets a single automation by ID.
/// </summary>
[ApiVersion("1.0")]
public sealed class ByIdAutomationController : AutomationControllerBase
{
    private readonly IAutomationService _automationService;
    private readonly IUmbracoMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="ByIdAutomationController"/> class.
    /// </summary>
    public ByIdAutomationController(IAutomationService automationService, IUmbracoMapper mapper)
    {
        _automationService = automationService;
        _mapper = mapper;
    }

    /// <summary>
    /// Gets an automation by its unique ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(AutomationResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAutomationById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var automation = await _automationService.GetAutomationAsync(id, cancellationToken);
        if (automation is null)
        {
            return AutomationNotFound();
        }

        return Ok(_mapper.Map<AutomationResponseModel>(automation));
    }
}
