using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Web.Api.Management.Workspace.Models;
using Umbraco.Cms.Api.Common.ViewModels.Pagination;
using Umbraco.Cms.Core.Mapping;

namespace Umbraco.Automate.Web.Api.Management.Workspace.Controllers;

/// <summary>
/// Gets all workspaces with optional paging and filtering.
/// </summary>
[ApiVersion("1.0")]
public sealed class AllWorkspaceController : WorkspaceControllerBase
{
    private readonly IWorkspaceService _workspaceService;
    private readonly IUmbracoMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="AllWorkspaceController"/> class.
    /// </summary>
    public AllWorkspaceController(IWorkspaceService workspaceService, IUmbracoMapper mapper)
    {
        _workspaceService = workspaceService;
        _mapper = mapper;
    }

    /// <summary>
    /// Gets a paged list of workspaces.
    /// </summary>
    [HttpGet]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(PagedViewModel<WorkspaceItemResponseModel>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedViewModel<WorkspaceItemResponseModel>>> GetAllWorkspaces(
        string? filter = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        var (items, total) = await _workspaceService.GetWorkspacesPagedAsync(filter, skip, take, cancellationToken);

        return Ok(new PagedViewModel<WorkspaceItemResponseModel>
        {
            Total = total,
            Items = _mapper.MapEnumerable<Core.Workspaces.Workspace, WorkspaceItemResponseModel>(items),
        });
    }
}
