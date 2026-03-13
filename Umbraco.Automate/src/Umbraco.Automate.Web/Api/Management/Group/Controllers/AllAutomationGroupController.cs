using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Web.Api.Management.Group.Models;
using Umbraco.Cms.Core.Mapping;

namespace Umbraco.Automate.Web.Api.Management.Group.Controllers;

/// <summary>
/// Gets all automation groups for a workspace.
/// </summary>
[ApiVersion("1.0")]
public sealed class AllAutomationGroupController : AutomationGroupControllerBase
{
    private readonly IAutomationGroupService _groupService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUmbracoMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="AllAutomationGroupController"/> class.
    /// </summary>
    public AllAutomationGroupController(
        IAutomationGroupService groupService,
        IAuthorizationService authorizationService,
        IUmbracoMapper mapper)
    {
        _groupService = groupService;
        _authorizationService = authorizationService;
        _mapper = mapper;
    }

    /// <summary>
    /// Gets all automation groups for a workspace.
    /// </summary>
    [HttpGet("workspace/{workspaceId:guid}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(IEnumerable<AutomationGroupResponseModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllAutomationGroups(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        var forbidden = await AuthorizeWorkspaceAccessAsync(_authorizationService, workspaceId);
        if (forbidden is not null)
        {
            return forbidden;
        }

        var groups = await _groupService.GetGroupsByWorkspaceAsync(workspaceId, cancellationToken);

        return Ok(_mapper.MapEnumerable<AutomationGroup, AutomationGroupResponseModel>(groups));
    }
}
