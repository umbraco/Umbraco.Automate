using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Web.Api.Management.Catalogue.Models;
using Umbraco.Cms.Core.Mapping;

namespace Umbraco.Automate.Web.Api.Management.Catalogue.Controllers;

/// <summary>
/// Gets all registered trigger types.
/// </summary>
[ApiVersion("1.0")]
public sealed class AllTriggersController : CatalogueControllerBase
{
    private readonly TriggerCollection _triggers;
    private readonly IUmbracoMapper _mapper;
    private readonly IWorkspaceServiceAccountResolver _serviceAccountResolver;
    private readonly ISectionAccessChecker _sectionAccessChecker;

    /// <summary>
    /// Initializes a new instance of the <see cref="AllTriggersController"/> class.
    /// </summary>
    public AllTriggersController(
        TriggerCollection triggers,
        IUmbracoMapper mapper,
        IWorkspaceServiceAccountResolver serviceAccountResolver,
        ISectionAccessChecker sectionAccessChecker)
    {
        _triggers = triggers;
        _mapper = mapper;
        _serviceAccountResolver = serviceAccountResolver;
        _sectionAccessChecker = sectionAccessChecker;
    }

    /// <summary>
    /// Gets all registered trigger types with their metadata and schemas. When
    /// <paramref name="workspaceId"/> is provided, the response is filtered to triggers
    /// the workspace's service account has the required backoffice section access for.
    /// </summary>
    [HttpGet("triggers")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(IEnumerable<TriggerItemResponseModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<TriggerItemResponseModel>>> GetAllTriggers(
        [FromQuery] Guid? workspaceId = null,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<ITrigger> triggers = _triggers;

        if (workspaceId.HasValue)
        {
            var serviceAccount = await _serviceAccountResolver.GetServiceAccountAsync(workspaceId.Value, cancellationToken);
            if (serviceAccount is null)
            {
                return NotFound();
            }

            triggers = triggers.Where(t => _sectionAccessChecker.CanAccess(serviceAccount, t));
        }

        return Ok(_mapper.MapEnumerable<ITrigger, TriggerItemResponseModel>(triggers));
    }
}
