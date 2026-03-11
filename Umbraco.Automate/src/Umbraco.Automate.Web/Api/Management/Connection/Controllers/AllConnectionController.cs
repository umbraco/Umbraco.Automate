using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Web.Api.Management.Connection.Models;
using Umbraco.Cms.Api.Common.ViewModels.Pagination;
using Umbraco.Cms.Core.Mapping;

namespace Umbraco.Automate.Web.Api.Management.Connection.Controllers;

/// <summary>
/// Gets all connections with optional paging and filtering.
/// </summary>
[ApiVersion("1.0")]
public sealed class AllConnectionController : ConnectionControllerBase
{
    private readonly IConnectionService _connectionService;
    private readonly IUmbracoMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="AllConnectionController"/> class.
    /// </summary>
    public AllConnectionController(IConnectionService connectionService, IUmbracoMapper mapper)
    {
        _connectionService = connectionService;
        _mapper = mapper;
    }

    /// <summary>
    /// Gets a paged list of connections.
    /// </summary>
    [HttpGet]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(PagedViewModel<ConnectionItemResponseModel>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedViewModel<ConnectionItemResponseModel>>> GetAllConnections(
        string? filter = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        var (items, total) = await _connectionService.GetConnectionsPagedAsync(filter, skip, take, cancellationToken);

        return Ok(new PagedViewModel<ConnectionItemResponseModel>
        {
            Total = total,
            Items = _mapper.MapEnumerable<Core.Connections.Connection, ConnectionItemResponseModel>(items),
        });
    }
}
