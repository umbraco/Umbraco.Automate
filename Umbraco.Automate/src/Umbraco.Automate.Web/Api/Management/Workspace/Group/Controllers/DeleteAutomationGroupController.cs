using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Automations;

namespace Umbraco.Automate.Web.Api.Management.Workspace.Group.Controllers;

/// <summary>
/// Deletes an automation group and all its contents (cascade).
/// </summary>
[ApiVersion("1.0")]
public sealed class DeleteAutomationGroupController : AutomationGroupControllerBase
{
    private readonly IAutomationGroupService _groupService;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteAutomationGroupController"/> class.
    /// </summary>
    public DeleteAutomationGroupController(
        IAutomationGroupService groupService,
        IAuthorizationService authorizationService)
    {
        _groupService = groupService;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Deletes an automation group and cascade-deletes all child groups and automations.
    /// </summary>
    [HttpDelete("{groupId:guid}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAutomationGroup(
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
