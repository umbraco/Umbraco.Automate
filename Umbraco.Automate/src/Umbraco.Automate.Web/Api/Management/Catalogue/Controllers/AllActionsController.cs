using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Web.Api.Management.Catalogue.Models;

namespace Umbraco.Automate.Web.Api.Management.Catalogue.Controllers;

/// <summary>
/// Gets all registered action types.
/// </summary>
[ApiVersion("1.0")]
public sealed class AllActionsController : CatalogueControllerBase
{
    private readonly ActionCollection _actions;

    /// <summary>
    /// Initializes a new instance of the <see cref="AllActionsController"/> class.
    /// </summary>
    public AllActionsController(ActionCollection actions)
    {
        _actions = actions;
    }

    /// <summary>
    /// Gets all registered action types with their metadata and schemas.
    /// </summary>
    [HttpGet("actions")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(IEnumerable<ActionItemResponseModel>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<ActionItemResponseModel>> GetAllActions()
    {
        var items = _actions.Select(a => new ActionItemResponseModel
        {
            Alias = a.Alias,
            Name = a.Name,
            Description = a.Description,
            Group = a.Group,
            Icon = a.Icon,
            SettingsSchema = a.GetSettingsSchema(),
        });

        return Ok(items);
    }
}
