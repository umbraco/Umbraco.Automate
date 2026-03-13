using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Web.Api.Management.Workspace.Group.Models;
using Umbraco.Cms.Core.Mapping;

namespace Umbraco.Automate.Web.Api.Management.Workspace.Group.Controllers;

/// <summary>
/// Creates a new automation group within a workspace.
/// </summary>
[ApiVersion("1.0")]
public sealed class CreateAutomationGroupController : AutomationGroupControllerBase
{
    private readonly IAutomationGroupService _groupService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUmbracoMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateAutomationGroupController"/> class.
    /// </summary>
    public CreateAutomationGroupController(
        IAutomationGroupService groupService,
        IAuthorizationService authorizationService,
        IUmbracoMapper mapper)
    {
        _groupService = groupService;
        _authorizationService = authorizationService;
        _mapper = mapper;
    }

    /// <summary>
    /// Creates a new automation group within a workspace.
    /// </summary>
    [HttpPost]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateAutomationGroup(
        Guid workspaceId,
        CreateAutomationGroupRequestModel requestModel,
        CancellationToken cancellationToken = default)
    {
        var forbidden = await AuthorizeWorkspaceAsync(_authorizationService, workspaceId);
        if (forbidden is not null)
        {
            return forbidden;
        }

        var group = _mapper.Map<AutomationGroup>(requestModel)!;
        group.WorkspaceId = workspaceId;

        try
        {
            var created = await _groupService.CreateGroupAsync(group, cancellationToken);

            return CreatedAtAction(
                nameof(ByIdAutomationGroupController.GetAutomationGroupById),
                nameof(ByIdAutomationGroupController).Replace("Controller", string.Empty),
                new { workspaceId, id = created.Id },
                created.Id.ToString());
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid operation", Detail = ex.Message });
        }
    }
}
