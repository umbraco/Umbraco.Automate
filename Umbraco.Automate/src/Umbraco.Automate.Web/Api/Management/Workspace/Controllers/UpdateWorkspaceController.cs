using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Web.Api.Management.Workspace.Models;
using Umbraco.Cms.Core.Mapping;

namespace Umbraco.Automate.Web.Api.Management.Workspace.Controllers;

/// <summary>
/// Updates an existing workspace.
/// </summary>
[ApiVersion("1.0")]
public sealed class UpdateWorkspaceController : WorkspaceControllerBase
{
    private readonly IWorkspaceService _workspaceService;
    private readonly IUmbracoMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateWorkspaceController"/> class.
    /// </summary>
    public UpdateWorkspaceController(IWorkspaceService workspaceService, IUmbracoMapper mapper)
    {
        _workspaceService = workspaceService;
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

        await _workspaceService.UpdateWorkspaceAsync(existing, cancellationToken: cancellationToken);

        return Ok();
    }
}
