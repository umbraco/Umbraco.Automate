using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Web.Api.Management.Workspace.Group.Models;
using Umbraco.Cms.Core.Mapping;

namespace Umbraco.Automate.Web.Api.Management.Workspace.Group.Controllers;

/// <summary>
/// Gets a single workspace group by ID.
/// </summary>
[ApiVersion("1.0")]
public sealed class ByIdWorkspaceGroupController : WorkspaceGroupControllerBase
{
    private readonly IWorkspaceGroupService _groupService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUmbracoMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="ByIdWorkspaceGroupController"/> class.
    /// </summary>
    public ByIdWorkspaceGroupController(
        IWorkspaceGroupService groupService,
        IAuthorizationService authorizationService,
        IUmbracoMapper mapper)
    {
        _groupService = groupService;
        _authorizationService = authorizationService;
        _mapper = mapper;
    }

    /// <summary>
    /// Gets a workspace group by its unique ID.
    /// </summary>
    [HttpGet("{groupId:guid}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(WorkspaceGroupResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWorkspaceGroupById(
        Guid id,
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        var forbidden = await AuthorizeWorkspaceAsync(_authorizationService, id);
        if (forbidden is not null)
        {
            return forbidden;
        }

        var group = await _groupService.GetGroupAsync(groupId, cancellationToken);
        if (group is null || group.WorkspaceId != id)
        {
            return GroupNotFound();
        }

        return Ok(_mapper.Map<WorkspaceGroupResponseModel>(group));
    }
}
