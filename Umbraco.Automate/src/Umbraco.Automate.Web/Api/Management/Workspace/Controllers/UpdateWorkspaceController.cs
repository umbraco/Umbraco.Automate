using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Web.Api.Management.Workspace.Models;
using Umbraco.Cms.Core.Mapping;
using Umbraco.Cms.Core.Security;

namespace Umbraco.Automate.Web.Api.Management.Workspace.Controllers;

/// <summary>
/// Updates an existing workspace.
/// </summary>
[ApiVersion("1.0")]
public sealed class UpdateWorkspaceController : WorkspaceControllerBase
{
    private readonly IWorkspaceService _workspaceService;
    private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;
    private readonly IUmbracoMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateWorkspaceController"/> class.
    /// </summary>
    public UpdateWorkspaceController(
        IWorkspaceService workspaceService,
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
        IUmbracoMapper mapper)
    {
        _workspaceService = workspaceService;
        _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
        _mapper = mapper;
    }

    /// <summary>
    /// Updates an existing workspace.
    /// </summary>
    [HttpPut("{id:guid}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateWorkspace(
        Guid id,
        UpdateWorkspaceRequestModel requestModel,
        CancellationToken cancellationToken = default)
    {
        var existing = await _workspaceService.GetWorkspaceAsync(id, cancellationToken);
        if (existing is null)
        {
            return WorkspaceNotFound();
        }

        _mapper.Map(requestModel, existing);

        await _workspaceService.UpdateWorkspaceAsync(existing, CurrentUserKey(_backOfficeSecurityAccessor), cancellationToken);

        return Ok();
    }
}
