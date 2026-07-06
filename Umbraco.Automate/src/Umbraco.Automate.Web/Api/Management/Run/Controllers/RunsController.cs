using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Web.Api.Management.Run.Models;
using Umbraco.Cms.Api.Common.ViewModels.Pagination;
using Umbraco.Cms.Core.Mapping;
using Umbraco.Cms.Core.Security.Authorization;
using Umbraco.Extensions;

namespace Umbraco.Automate.Web.Api.Management.Run.Controllers;

/// <summary>
/// Lists runs across all automations. Results are scoped to workspaces the current user has access to.
/// </summary>
[ApiVersion("1.0")]
public sealed class RunsController : RunControllerBase
{
    private readonly IAutomationRunService _runService;
    private readonly IWorkspaceService _workspaceService;
    private readonly IAuthorizationHelper _authorizationHelper;
    private readonly IUmbracoMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="RunsController"/> class.
    /// </summary>
    public RunsController(
        IAutomationRunService runService,
        IWorkspaceService workspaceService,
        IAuthorizationHelper authorizationHelper,
        IUmbracoMapper mapper)
    {
        _runService = runService;
        _workspaceService = workspaceService;
        _authorizationHelper = authorizationHelper;
        _mapper = mapper;
    }

    /// <summary>
    /// Gets a paged list of runs across all automations, newest first. Results are scoped to
    /// workspaces the current user has access to.
    /// </summary>
    [HttpGet]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(PagedViewModel<AutomationRunListItemResponseModel>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedViewModel<AutomationRunListItemResponseModel>>> GetRuns(
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        // Admins see all runs; non-admins see only runs in workspaces their groups can access.
        IReadOnlySet<Guid>? workspaceIds = null;

        var user = _authorizationHelper.GetUmbracoUser(User);
        if (!user.IsAdmin())
        {
            var userGroupKeys = user.Groups.Select(g => g.Key).ToList();
            workspaceIds = await _workspaceService.GetAccessibleWorkspaceIdsAsync(userGroupKeys, cancellationToken);
        }

        var (items, total) = await _runService.GetRunsPagedAsync(workspaceIds, skip, take, cancellationToken);

        return Ok(new PagedViewModel<AutomationRunListItemResponseModel>
        {
            Total = total,
            Items = _mapper.MapEnumerable<AutomationRunListItem, AutomationRunListItemResponseModel>(items),
        });
    }
}
