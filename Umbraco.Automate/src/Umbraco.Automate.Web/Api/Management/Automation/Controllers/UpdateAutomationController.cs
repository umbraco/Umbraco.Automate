using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Web.Api.Management.Automation.Models;
using Umbraco.Cms.Core.Mapping;

namespace Umbraco.Automate.Web.Api.Management.Automation.Controllers;

/// <summary>
/// Updates an existing automation.
/// </summary>
[ApiVersion("1.0")]
public sealed class UpdateAutomationController : AutomationControllerBase
{
    private readonly IAutomationService _automationService;
    private readonly IUmbracoMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateAutomationController"/> class.
    /// </summary>
    public UpdateAutomationController(IAutomationService automationService, IUmbracoMapper mapper)
    {
        _automationService = automationService;
        _mapper = mapper;
    }

    /// <summary>
    /// Updates an existing automation.
    /// </summary>
    [HttpPut("{id:guid}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAutomation(
        Guid id,
        UpdateAutomationRequestModel requestModel,
        CancellationToken cancellationToken = default)
    {
        var existing = await _automationService.GetAutomationAsync(id, cancellationToken);
        if (existing is null)
        {
            return AutomationNotFound();
        }

        _mapper.Map(requestModel, existing);

        await _automationService.UpdateAutomationAsync(existing, cancellationToken: cancellationToken);

        return Ok();
    }
}
