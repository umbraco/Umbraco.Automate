using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Cms.Core.Security;

namespace Umbraco.Automate.Web.Api.Management.Workspace.Controllers;

/// <summary>
/// Deletes a workspace.
/// </summary>
[ApiVersion("1.0")]
public sealed class DeleteWorkspaceController : WorkspaceControllerBase
{
    private readonly IWorkspaceService _workspaceService;
    private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteWorkspaceController"/> class.
    /// </summary>
    public DeleteWorkspaceController(
        IWorkspaceService workspaceService,
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor)
    {
        _workspaceService = workspaceService;
        _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
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
        var adminRequired = RequireAdmin(_backOfficeSecurityAccessor);
        if (adminRequired is not null)
        {
            return adminRequired;
        }

        var deleted = await _workspaceService.DeleteWorkspaceAsync(id, cancellationToken);
        if (!deleted)
        {
            return WorkspaceNotFound();
        }

        return Ok();
    }
}
