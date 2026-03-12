using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Web.Api.Management.Automation.Models;
using Umbraco.Cms.Core.Mapping;

namespace Umbraco.Automate.Web.Api.Management.Automation.Controllers;

/// <summary>
/// Creates a new automation.
/// </summary>
[ApiVersion("1.0")]
public sealed class CreateAutomationController : AutomationControllerBase
{
    private readonly IAutomationService _automationService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUmbracoMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateAutomationController"/> class.
    /// </summary>
    public CreateAutomationController(
        IAutomationService automationService,
        IAuthorizationService authorizationService,
        IUmbracoMapper mapper)
    {
        _automationService = automationService;
        _authorizationService = authorizationService;
        _mapper = mapper;
    }

    /// <summary>
    /// Creates a new automation.
    /// </summary>
    [HttpPost]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateAutomation(
        CreateAutomationRequestModel requestModel,
        CancellationToken cancellationToken = default)
    {
        var automation = _mapper.Map<Core.Automations.Automation>(requestModel)!;

        var forbidden = await AuthorizeWorkspaceAccessAsync(_authorizationService, automation.WorkspaceId);
        if (forbidden is not null)
        {
            return forbidden;
        }

        var created = await _automationService.CreateAutomationAsync(automation, cancellationToken: cancellationToken);

        return CreatedAtAction(
            nameof(ByIdAutomationController.GetAutomationById),
            nameof(ByIdAutomationController).Replace("Controller", string.Empty),
            new { id = created.Id },
            created.Id.ToString());
    }
}
