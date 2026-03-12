using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
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
    private readonly IAuthorizationService _authorizationService;
    private readonly IUmbracoMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateAutomationController"/> class.
    /// </summary>
    public UpdateAutomationController(
        IAutomationService automationService,
        IAuthorizationService authorizationService,
        IUmbracoMapper mapper)
    {
        _automationService = automationService;
        _authorizationService = authorizationService;
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

        var forbidden = await AuthorizeWorkspaceAccessAsync(_authorizationService, existing.WorkspaceId);
        if (forbidden is not null)
        {
            return forbidden;
        }

        _mapper.Map(requestModel, existing);

        await _automationService.UpdateAutomationAsync(existing, cancellationToken: cancellationToken);

        return Ok();
    }
}
