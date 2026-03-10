using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Actions;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="AllActionsController"/> class.
    /// </summary>
    public AllActionsController(ActionCollection actions, IUmbracoMapper mapper)
    {
        _actions = actions;
        _mapper = mapper;
    }

    /// <summary>
    /// Gets all registered action types with their metadata and schemas.
    /// </summary>
    [HttpGet("actions")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(IEnumerable<ActionItemResponseModel>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<ActionItemResponseModel>> GetAllActions()
    {
        return Ok(_mapper.MapEnumerable<IAction, ActionItemResponseModel>(_actions));
    }
}
