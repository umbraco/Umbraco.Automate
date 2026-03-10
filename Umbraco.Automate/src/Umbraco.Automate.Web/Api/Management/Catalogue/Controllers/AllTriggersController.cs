using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="AllTriggersController"/> class.
    /// </summary>
    public AllTriggersController(TriggerCollection triggers, IUmbracoMapper mapper)
    {
        _triggers = triggers;
        _mapper = mapper;
    }

    /// <summary>
    /// Gets all registered trigger types with their metadata and schemas.
    /// </summary>
    [HttpGet("triggers")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(IEnumerable<TriggerItemResponseModel>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<TriggerItemResponseModel>> GetAllTriggers()
    {
        return Ok(_mapper.MapEnumerable<ITrigger, TriggerItemResponseModel>(_triggers));
    }
}
