using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Web.Api.Management.Catalogue.Models;
using Umbraco.Cms.Core.Mapping;

namespace Umbraco.Automate.Web.Api.Management.Catalogue.Controllers;

/// <summary>
/// Gets all registered action types.
/// </summary>
[ApiVersion("1.0")]
public sealed class AllActionsController : CatalogueControllerBase
{
    private readonly ActionCollection _actions;
    private readonly IUmbracoMapper _mapper;
    private readonly IWorkspaceServiceAccountResolver _serviceAccountResolver;
    private readonly ISectionAccessChecker _sectionAccessChecker;

    /// <summary>
    /// Initializes a new instance of the <see cref="AllActionsController"/> class.
    /// </summary>
    public AllActionsController(
        ActionCollection actions,
        IUmbracoMapper mapper,
        IWorkspaceServiceAccountResolver serviceAccountResolver,
        ISectionAccessChecker sectionAccessChecker)
    {
        _actions = actions;
        _mapper = mapper;
        _serviceAccountResolver = serviceAccountResolver;
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
            var serviceAccount = await _serviceAccountResolver.GetServiceAccountAsync(workspaceId.Value, cancellationToken);
            if (serviceAccount is null)
            {
                return NotFound();
            }

            actions = actions.Where(a =>
                _sectionAccessChecker.CanAccess(serviceAccount, a)
                && _sectionAccessChecker.HasRequiredPermissions(serviceAccount, a));
        }

        return Ok(_mapper.MapEnumerable<IAction, ActionItemResponseModel>(actions));
    }
}
