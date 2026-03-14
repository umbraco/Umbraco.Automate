using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.ControlFlow;
using Umbraco.Automate.Web.Api.Management.Catalogue.Models;
using Umbraco.Cms.Core.Mapping;

namespace Umbraco.Automate.Web.Api.Management.Catalogue.Controllers;

/// <summary>
/// Gets all registered control flow types.
/// </summary>
[ApiVersion("1.0")]
public sealed class AllControlFlowController : CatalogueControllerBase
{
    private readonly ControlFlowCollection _controlFlow;
    private readonly IUmbracoMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="AllControlFlowController"/> class.
    /// </summary>
    public AllControlFlowController(ControlFlowCollection controlFlow, IUmbracoMapper mapper)
    {
        _controlFlow = controlFlow;
        _mapper = mapper;
    }

    /// <summary>
    /// Gets all registered control flow types with their metadata and schemas.
    /// </summary>
    [HttpGet("control-flow")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(IEnumerable<ControlFlowItemResponseModel>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<ControlFlowItemResponseModel>> GetAllControlFlow()
    {
        return Ok(_mapper.MapEnumerable<IControlFlow, ControlFlowItemResponseModel>(_controlFlow));
    }
}
