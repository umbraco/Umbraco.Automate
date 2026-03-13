using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Web.Api.Management.Group.Models;
using Umbraco.Cms.Core.Mapping;

namespace Umbraco.Automate.Web.Api.Management.Group.Controllers;

/// <summary>
/// Updates an existing automation group.
/// </summary>
[ApiVersion("1.0")]
public sealed class UpdateAutomationGroupController : AutomationGroupControllerBase
{
    private readonly IAutomationGroupService _groupService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUmbracoMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateAutomationGroupController"/> class.
    /// </summary>
    public UpdateAutomationGroupController(
        IAutomationGroupService groupService,
        IAuthorizationService authorizationService,
        IUmbracoMapper mapper)
    {
        _groupService = groupService;
        _authorizationService = authorizationService;
        _mapper = mapper;
    }

    /// <summary>
    /// Updates an automation group (rename or move).
    /// </summary>
    [HttpPut("{id:guid}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateAutomationGroup(
        Guid id,
        UpdateAutomationGroupRequestModel requestModel,
        CancellationToken cancellationToken = default)
    {
        var existing = await _groupService.GetGroupAsync(id, cancellationToken);
        if (existing is null)
        {
            return GroupNotFound();
        }

        var forbidden = await AuthorizeWorkspaceAccessAsync(_authorizationService, existing.WorkspaceId);
        if (forbidden is not null)
        {
            return forbidden;
        }

        var group = _mapper.Map<AutomationGroup>(requestModel)!;
        group.Id = id;

        try
        {
            await _groupService.UpdateGroupAsync(group, cancellationToken);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid operation", Detail = ex.Message });
        }
    }
}
