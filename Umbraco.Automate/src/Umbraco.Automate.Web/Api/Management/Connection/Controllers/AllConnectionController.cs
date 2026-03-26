using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Web.Api.Management.Connection.Models;
using Umbraco.Cms.Api.Common.ViewModels.Pagination;
using Umbraco.Cms.Core.Mapping;
using Umbraco.Cms.Core.Security;

namespace Umbraco.Automate.Web.Api.Management.Connection.Controllers;

/// <summary>
/// Gets all connections with optional paging and filtering.
/// </summary>
[ApiVersion("1.0")]
public sealed class AllConnectionController : ConnectionControllerBase
{
    private readonly IConnectionService _connectionService;
    private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;
    private readonly IUmbracoMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="AllConnectionController"/> class.
    /// </summary>
    public AllConnectionController(
        IConnectionService connectionService,
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
        IUmbracoMapper mapper)
    {
        _connectionService = connectionService;
        _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
        _mapper = mapper;
    }

    /// <summary>
    /// Gets a paged list of connections.
    /// </summary>
    [HttpGet]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(PagedViewModel<ConnectionItemResponseModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllConnections(
        string? filter = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        var adminRequired = RequireAdmin(_backOfficeSecurityAccessor);
        if (adminRequired is not null)
        {
            return adminRequired;
        }

        var (items, total) = await _connectionService.GetConnectionsPagedAsync(filter, skip, take, cancellationToken);

        return Ok(new PagedViewModel<ConnectionItemResponseModel>
        {
            Total = total,
            Items = _mapper.MapEnumerable<Core.Connections.Connection, ConnectionItemResponseModel>(items),
        });
    }
}
