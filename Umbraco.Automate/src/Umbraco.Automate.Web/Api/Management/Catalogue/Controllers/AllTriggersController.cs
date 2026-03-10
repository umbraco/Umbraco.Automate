using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Web.Api.Management.Catalogue.Models;

namespace Umbraco.Automate.Web.Api.Management.Catalogue.Controllers;

/// <summary>
/// Gets all registered trigger types.
/// </summary>
[ApiVersion("1.0")]
public sealed class AllTriggersController : CatalogueControllerBase
{
    private readonly TriggerCollection _triggers;

    /// <summary>
    /// Initializes a new instance of the <see cref="AllTriggersController"/> class.
    /// </summary>
    public AllTriggersController(TriggerCollection triggers)
    {
        _triggers = triggers;
    }

    /// <summary>
    /// Gets all registered trigger types with their metadata and schemas.
    /// </summary>
    [HttpGet("triggers")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(IEnumerable<TriggerItemResponseModel>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<TriggerItemResponseModel>> GetAllTriggers()
    {
        var items = _triggers.Select(t => new TriggerItemResponseModel
        {
            Alias = t.Alias,
            Name = t.Name,
            Description = t.Description,
            Group = t.Group,
            Icon = t.Icon,
            SettingsSchema = t.GetSettingsSchema(),
            OutputProperties = t.GetOutputProperties(),
        });

        return Ok(items);
    }
}
