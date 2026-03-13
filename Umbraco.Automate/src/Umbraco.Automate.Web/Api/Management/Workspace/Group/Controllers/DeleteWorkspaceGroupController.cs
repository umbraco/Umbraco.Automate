using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Workspaces;

namespace Umbraco.Automate.Web.Api.Management.Workspace.Group.Controllers;

/// <summary>
/// Deletes a workspace group and all its contents (cascade).
/// </summary>
[ApiVersion("1.0")]
public sealed class DeleteWorkspaceGroupController : WorkspaceGroupControllerBase
{
    private readonly IWorkspaceGroupService _groupService;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteWorkspaceGroupController"/> class.
    /// </summary>
    public DeleteWorkspaceGroupController(
        IWorkspaceGroupService groupService,
        IAuthorizationService authorizationService)
    {
        _groupService = groupService;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Deletes a workspace group and cascade-deletes all child groups and automations.
    /// </summary>
    [HttpDelete("{groupId:guid}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteWorkspaceGroup(
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

        await _groupService.DeleteGroupAsync(groupId, cancellationToken);

        return Ok();
    }
}
