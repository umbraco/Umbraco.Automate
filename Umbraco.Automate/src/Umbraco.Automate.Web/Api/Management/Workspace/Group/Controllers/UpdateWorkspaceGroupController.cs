using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Web.Api.Management.Workspace.Group.Models;
using Umbraco.Cms.Core.Mapping;

namespace Umbraco.Automate.Web.Api.Management.Workspace.Group.Controllers;

/// <summary>
/// Updates an existing workspace group.
/// </summary>
[ApiVersion("1.0")]
public sealed class UpdateWorkspaceGroupController : WorkspaceGroupControllerBase
{
    private readonly IWorkspaceGroupService _groupService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUmbracoMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateWorkspaceGroupController"/> class.
    /// </summary>
    public UpdateWorkspaceGroupController(
        IWorkspaceGroupService groupService,
        IAuthorizationService authorizationService,
        IUmbracoMapper mapper)
    {
        _groupService = groupService;
        _authorizationService = authorizationService;
        _mapper = mapper;
    }

    /// <summary>
    /// Updates a workspace group (rename or move).
    /// </summary>
    [HttpPut("{groupId:guid}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateWorkspaceGroup(
        Guid id,
        Guid groupId,
        UpdateWorkspaceGroupRequestModel requestModel,
        CancellationToken cancellationToken = default)
    {
        var forbidden = await AuthorizeWorkspaceAsync(_authorizationService, id);
        if (forbidden is not null)
        {
            return forbidden;
        }

        var existing = await _groupService.GetGroupAsync(groupId, cancellationToken);
        if (existing is null || existing.WorkspaceId != id)
        {
            return GroupNotFound();
        }

        var group = _mapper.Map<WorkspaceGroup>(requestModel)!;
        group.Id = groupId;

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
