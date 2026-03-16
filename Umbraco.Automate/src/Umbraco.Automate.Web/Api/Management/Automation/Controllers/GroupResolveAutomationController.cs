using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Web.Api.Management.Workspace.Group.Models;
using Umbraco.Cms.Core.Mapping;

namespace Umbraco.Automate.Web.Api.Management.Automation.Controllers;

/// <summary>
/// Resolves a workspace group by ID without requiring the workspace ID upfront.
/// </summary>
[ApiVersion("1.0")]
public sealed class GroupResolveAutomationController : AutomationControllerBase
{
    private readonly IWorkspaceGroupService _groupService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUmbracoMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="GroupResolveAutomationController"/> class.
    /// </summary>
    public GroupResolveAutomationController(
        IWorkspaceGroupService groupService,
        IAuthorizationService authorizationService,
        IUmbracoMapper mapper)
    {
        _groupService = groupService;
        _authorizationService = authorizationService;
        _mapper = mapper;
    }

    /// <summary>
    /// Gets a workspace group by its ID, including its workspace ID.
    /// </summary>
    [HttpGet("groups/{groupId:guid}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(WorkspaceGroupResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGroupById(
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        var group = await _groupService.GetGroupAsync(groupId, cancellationToken);
        if (group is null)
        {
            return AutomationNotFound();
        }

        var forbidden = await AuthorizeWorkspaceAccessAsync(_authorizationService, group.WorkspaceId);
        if (forbidden is not null)
        {
            return forbidden;
        }

        return Ok(_mapper.Map<WorkspaceGroupResponseModel>(group));
    }
}
