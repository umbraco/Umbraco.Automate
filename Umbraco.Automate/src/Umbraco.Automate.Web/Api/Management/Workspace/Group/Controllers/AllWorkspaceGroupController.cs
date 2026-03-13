using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Web.Api.Management.Workspace.Group.Models;
using Umbraco.Cms.Core.Mapping;

namespace Umbraco.Automate.Web.Api.Management.Workspace.Group.Controllers;

/// <summary>
/// Gets all workspace groups for a workspace.
/// </summary>
[ApiVersion("1.0")]
public sealed class AllWorkspaceGroupController : WorkspaceGroupControllerBase
{
    private readonly IWorkspaceGroupService _groupService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUmbracoMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="AllWorkspaceGroupController"/> class.
    /// </summary>
    public AllWorkspaceGroupController(
        IWorkspaceGroupService groupService,
        IAuthorizationService authorizationService,
        IUmbracoMapper mapper)
    {
        _groupService = groupService;
        _authorizationService = authorizationService;
        _mapper = mapper;
    }

    /// <summary>
    /// Gets all workspace groups for a workspace.
    /// </summary>
    [HttpGet]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(IEnumerable<WorkspaceGroupResponseModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllWorkspaceGroups(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var forbidden = await AuthorizeWorkspaceAsync(_authorizationService, id);
        if (forbidden is not null)
        {
            return forbidden;
        }

        var groups = await _groupService.GetGroupsByWorkspaceAsync(id, cancellationToken);

        return Ok(_mapper.MapEnumerable<WorkspaceGroup, WorkspaceGroupResponseModel>(groups));
    }
}
