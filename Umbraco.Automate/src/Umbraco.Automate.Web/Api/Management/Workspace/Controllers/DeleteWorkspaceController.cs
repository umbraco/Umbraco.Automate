using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Workspaces;

namespace Umbraco.Automate.Web.Api.Management.Workspace.Controllers;

/// <summary>
/// Deletes a workspace.
/// </summary>
[ApiVersion("1.0")]
public sealed class DeleteWorkspaceController : WorkspaceControllerBase
{
    private readonly IWorkspaceService _workspaceService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteWorkspaceController"/> class.
    /// </summary>
    public DeleteWorkspaceController(IWorkspaceService workspaceService)
    {
        _workspaceService = workspaceService;
    }

    /// <summary>
    /// Deletes a workspace.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteWorkspace(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var deleted = await _workspaceService.DeleteWorkspaceAsync(id, cancellationToken);
        if (!deleted)
        {
            return WorkspaceNotFound();
        }

        return Ok();
    }
}
