using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Web.Api.Management.Catalogue.Models;
using Umbraco.Cms.Core.Mapping;

namespace Umbraco.Automate.Web.Api.Management.Catalogue.Controllers;

/// <summary>
/// Gets all registered connection types.
/// </summary>
[ApiVersion("1.0")]
public sealed class AllConnectionTypesController : CatalogueControllerBase
{
    private readonly ConnectionTypeCollection _connectionTypes;
    private readonly IUmbracoMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="AllConnectionTypesController"/> class.
    /// </summary>
    public AllConnectionTypesController(ConnectionTypeCollection connectionTypes, IUmbracoMapper mapper)
    {
        _connectionTypes = connectionTypes;
        _mapper = mapper;
    }

    /// <summary>
    /// Gets all registered connection types with their metadata and schemas.
    /// </summary>
    [HttpGet("connection-types")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(IEnumerable<ConnectionTypeItemResponseModel>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<ConnectionTypeItemResponseModel>> GetAllConnectionTypes()
    {
        return Ok(_mapper.MapEnumerable<IConnectionType, ConnectionTypeItemResponseModel>(_connectionTypes));
    }
}
