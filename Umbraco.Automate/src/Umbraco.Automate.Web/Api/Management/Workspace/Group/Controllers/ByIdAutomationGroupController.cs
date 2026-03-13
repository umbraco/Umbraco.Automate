using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Web.Api.Management.Workspace.Group.Models;
using Umbraco.Cms.Core.Mapping;

namespace Umbraco.Automate.Web.Api.Management.Workspace.Group.Controllers;

/// <summary>
/// Gets a single automation group by ID.
/// </summary>
[ApiVersion("1.0")]
public sealed class ByIdAutomationGroupController : AutomationGroupControllerBase
{
    private readonly IAutomationGroupService _groupService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUmbracoMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="ByIdAutomationGroupController"/> class.
    /// </summary>
    public ByIdAutomationGroupController(
        IAutomationGroupService groupService,
        IAuthorizationService authorizationService,
        IUmbracoMapper mapper)
    {
        _groupService = groupService;
        _authorizationService = authorizationService;
        _mapper = mapper;
    }

    /// <summary>
    /// Gets an automation group by its unique ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(AutomationGroupResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAutomationGroupById(
        Guid workspaceId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var forbidden = await AuthorizeWorkspaceAsync(_authorizationService, workspaceId);
        if (forbidden is not null)
        {
            return forbidden;
        }

        var group = await _groupService.GetGroupAsync(id, cancellationToken);
        if (group is null || group.WorkspaceId != workspaceId)
        {
            return GroupNotFound();
        }

        return Ok(_mapper.Map<AutomationGroupResponseModel>(group));
    }
}
