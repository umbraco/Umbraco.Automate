using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Web.Api.Management.Workspace.Models;
using Umbraco.Cms.Api.Common.ViewModels.Pagination;
using Umbraco.Cms.Core.Mapping;
using Umbraco.Cms.Core.Security;
using Umbraco.Extensions;

namespace Umbraco.Automate.Web.Api.Management.Workspace.Controllers;

/// <summary>
/// Gets all workspaces with optional paging and filtering.
/// Non-admin users only see workspaces where their user groups have been granted access.
/// </summary>
[ApiVersion("1.0")]
public sealed class AllWorkspaceController : WorkspaceControllerBase
{
    private readonly IWorkspaceService _workspaceService;
    private readonly IUmbracoMapper _mapper;
    private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="AllWorkspaceController"/> class.
    /// </summary>
    public AllWorkspaceController(
        IWorkspaceService workspaceService,
        IUmbracoMapper mapper,
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor)
    {
        _workspaceService = workspaceService;
        _mapper = mapper;
        _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
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
        // Admins see all workspaces; non-admins only see workspaces their groups have access to.
        var user = _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser;
        if (user is null)
        {
            return Unauthorized();
        }

        IReadOnlyCollection<Guid>? userGroupKeys = null;
        if (!user.IsAdmin())
        {
            userGroupKeys = user.Groups.Select(g => g.Key).ToList();
        }

        var (items, total) = await _workspaceService.GetWorkspacesPagedAsync(filter, userGroupKeys, skip, take, cancellationToken);

        return Ok(new PagedViewModel<WorkspaceItemResponseModel>
        {
            Total = total,
            Items = _mapper.MapEnumerable<Core.Workspaces.Workspace, WorkspaceItemResponseModel>(items),
        });
    }
}
