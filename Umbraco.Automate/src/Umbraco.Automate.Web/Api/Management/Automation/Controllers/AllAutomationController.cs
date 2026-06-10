using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Web.Api.Management.Automation.Models;
using Umbraco.Cms.Api.Common.ViewModels.Pagination;
using Umbraco.Cms.Core.Mapping;
using Umbraco.Cms.Core.Security.Authorization;
using Umbraco.Extensions;

namespace Umbraco.Automate.Web.Api.Management.Automation.Controllers;

/// <summary>
/// Gets all automations with optional paging and filtering.
/// Results are scoped to workspaces the current user has access to.
/// </summary>
[ApiVersion("1.0")]
public sealed class AllAutomationController : AutomationControllerBase
{
    private readonly IAutomationService _automationService;
    private readonly IWorkspaceService _workspaceService;
    private readonly IAuthorizationHelper _authorizationHelper;
    private readonly ICircuitBreakerService _circuitBreaker;
    private readonly IUmbracoMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="AllAutomationController"/> class.
    /// </summary>
    public AllAutomationController(
        IAutomationService automationService,
        IWorkspaceService workspaceService,
        IAuthorizationHelper authorizationHelper,
        ICircuitBreakerService circuitBreaker,
        IUmbracoMapper mapper)
    {
        _automationService = automationService;
        _workspaceService = workspaceService;
        _authorizationHelper = authorizationHelper;
        _circuitBreaker = circuitBreaker;
        _mapper = mapper;
    }

    /// <summary>
    /// Gets a paged list of automations. Results are scoped to workspaces the current user has access to.
    /// </summary>
    [HttpGet]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(PagedViewModel<AutomationItemResponseModel>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedViewModel<AutomationItemResponseModel>>> GetAllAutomations(
        string? filter = null,
        Guid? groupId = null,
        Guid? workspaceId = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        // Admins see all automations; non-admins see only automations in their workspaces.
        IReadOnlySet<Guid>? workspaceIds = null;

        var user = _authorizationHelper.GetUmbracoUser(User);
        if (!user.IsAdmin())
        {
            var userGroupKeys = user.Groups.Select(g => g.Key).ToList();
            workspaceIds = await _workspaceService.GetAccessibleWorkspaceIdsAsync(userGroupKeys, cancellationToken);
        }

        // If a specific workspace is requested, intersect with the access-scoped set.
        if (workspaceId.HasValue)
        {
            var requested = new HashSet<Guid> { workspaceId.Value };
            workspaceIds = workspaceIds is not null
                ? (IReadOnlySet<Guid>)requested.Intersect(workspaceIds).ToHashSet()
                : requested;
        }

        var (items, total) = await _automationService.GetAutomationsPagedAsync(filter, workspaceIds, groupId, skip, take, cancellationToken);

        var itemList = items.ToList();
        var models = _mapper.MapEnumerable<Core.Automations.Automation, AutomationItemResponseModel>(itemList);

        var healthMap = await _circuitBreaker.GetHealthAsync(itemList.Select(a => a.Id).ToList(), cancellationToken);
        foreach (var model in models)
        {
            model.Health = healthMap[model.Id].Health;
        }

        return Ok(new PagedViewModel<AutomationItemResponseModel>
        {
            Total = total,
            Items = models,
        });
    }
}
