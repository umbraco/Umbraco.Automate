using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Web.Api.Management.Connection.Models;
using Umbraco.Cms.Core.Mapping;

namespace Umbraco.Automate.Web.Api.Management.Connection.Controllers;

/// <summary>
/// Gets a single connection by ID.
/// </summary>
[ApiVersion("1.0")]
public sealed class ByIdConnectionController : ConnectionControllerBase
{
    private readonly IConnectionService _connectionService;
    private readonly IUmbracoMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="ByIdConnectionController"/> class.
    /// </summary>
    public ByIdConnectionController(IConnectionService connectionService, IUmbracoMapper mapper)
    {
        _connectionService = connectionService;
        _mapper = mapper;
    }

    /// <summary>
    /// Gets a connection by its unique ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(ConnectionResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetConnectionById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var connection = await _connectionService.GetConnectionAsync(id, cancellationToken);
        if (connection is null)
        {
            return ConnectionNotFound();
        }

        return Ok(_mapper.Map<ConnectionResponseModel>(connection));
    }
}
