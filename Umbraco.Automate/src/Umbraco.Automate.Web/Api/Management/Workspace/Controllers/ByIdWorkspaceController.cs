using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Web.Api.Management.Workspace.Models;
using Umbraco.Cms.Core.Mapping;

namespace Umbraco.Automate.Web.Api.Management.Workspace.Controllers;

/// <summary>
/// Gets a single workspace by ID.
/// </summary>
[ApiVersion("1.0")]
public sealed class ByIdWorkspaceController : WorkspaceControllerBase
{
    private readonly IWorkspaceService _workspaceService;
    private readonly IUmbracoMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="ByIdWorkspaceController"/> class.
    /// </summary>
    public ByIdWorkspaceController(IWorkspaceService workspaceService, IUmbracoMapper mapper)
    {
        _workspaceService = workspaceService;
        _mapper = mapper;
    }

    /// <summary>
    /// Gets a workspace by its unique ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(WorkspaceResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWorkspaceById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var workspace = await _workspaceService.GetWorkspaceAsync(id, cancellationToken);
        if (workspace is null)
        {
            return WorkspaceNotFound();
        }

        return Ok(_mapper.Map<WorkspaceResponseModel>(workspace));
    }
}
