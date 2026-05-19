using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Web.Api.Management.Catalogue.Models;
using Umbraco.Cms.Core.Mapping;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Automate.Web.Api.Management.Catalogue.Controllers;

/// <summary>
/// Gets all registered action types.
/// </summary>
[ApiVersion("1.0")]
public sealed class AllActionsController : CatalogueControllerBase
{
    private readonly ActionCollection _actions;
    private readonly IUmbracoMapper _mapper;
    private readonly IWorkspaceService _workspaceService;
    private readonly IUserService _userService;
    private readonly ISectionAccessChecker _sectionAccessChecker;

    /// <summary>
    /// Initializes a new instance of the <see cref="AllActionsController"/> class.
    /// </summary>
    public AllActionsController(
        ActionCollection actions,
        IUmbracoMapper mapper,
        IWorkspaceService workspaceService,
        IUserService userService,
        ISectionAccessChecker sectionAccessChecker)
    {
        _actions = actions;
        _mapper = mapper;
        _workspaceService = workspaceService;
        _userService = userService;
        _sectionAccessChecker = sectionAccessChecker;
    }

    /// <summary>
    /// Gets all registered action types with their metadata and schemas. When
    /// <paramref name="workspaceId"/> is provided, the response is filtered to actions
    /// the workspace's service account has the required backoffice section access for.
    /// </summary>
    [HttpGet("actions")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(IEnumerable<ActionItemResponseModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<ActionItemResponseModel>>> GetAllActions(
        [FromQuery] Guid? workspaceId = null,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<IAction> actions = _actions;

        if (workspaceId.HasValue)
        {
            var workspace = await _workspaceService.GetWorkspaceAsync(workspaceId.Value, cancellationToken);
            if (workspace is null)
            {
                return NotFound();
            }

            var serviceAccount = workspace.ServiceAccountKey == Guid.Empty
                ? null
                : await _userService.GetAsync(workspace.ServiceAccountKey);

            if (serviceAccount is null)
            {
                return NotFound();
            }

            actions = actions.Where(a => _sectionAccessChecker.CanAccess(serviceAccount, a));
        }

        return Ok(_mapper.MapEnumerable<IAction, ActionItemResponseModel>(actions));
    }
}
