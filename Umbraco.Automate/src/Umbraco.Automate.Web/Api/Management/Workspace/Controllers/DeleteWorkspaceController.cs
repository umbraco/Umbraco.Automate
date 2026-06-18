using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Cms.Api.Common.Builders;
using Umbraco.Cms.Core.Security;

namespace Umbraco.Automate.Web.Api.Management.Workspace.Controllers;

/// <summary>
/// Deletes a workspace.
/// </summary>
[ApiVersion("1.0")]
public sealed class DeleteWorkspaceController : WorkspaceControllerBase
{
    private readonly IWorkspaceService _workspaceService;
    private readonly IAutomationService _automationService;
    private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteWorkspaceController"/> class.
    /// </summary>
    public DeleteWorkspaceController(
        IWorkspaceService workspaceService,
        IAutomationService automationService,
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor)
    {
        _workspaceService = workspaceService;
        _automationService = automationService;
        _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
    }

    /// <summary>
    /// Deletes a workspace.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteWorkspace(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var adminRequired = RequireAdmin(_backOfficeSecurityAccessor);
        if (adminRequired is not null)
        {
            return adminRequired;
        }

        if (await _automationService.ExistsByWorkspaceAsync(id, cancellationToken))
        {
            return Conflict(new ProblemDetailsBuilder()
                .WithTitle("Workspace not empty")
                .WithDetail("Cannot delete a workspace that contains automations. Delete or move the automations first.")
                .Build());
        }

        var deleted = await _workspaceService.DeleteWorkspaceAsync(id, cancellationToken);
        if (!deleted)
        {
            return WorkspaceNotFound();
        }

        return Ok();
    }
}
