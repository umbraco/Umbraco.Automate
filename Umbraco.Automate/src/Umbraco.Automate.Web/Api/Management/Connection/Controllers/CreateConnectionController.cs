using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Web.Api.Management.Connection.Models;
using Umbraco.Cms.Core.Mapping;

namespace Umbraco.Automate.Web.Api.Management.Connection.Controllers;

/// <summary>
/// Creates a new connection.
/// </summary>
[ApiVersion("1.0")]
public sealed class CreateConnectionController : ConnectionControllerBase
{
    private readonly IConnectionService _connectionService;
    private readonly IUmbracoMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateConnectionController"/> class.
    /// </summary>
    public CreateConnectionController(IConnectionService connectionService, IUmbracoMapper mapper)
    {
        _connectionService = connectionService;
        _mapper = mapper;
    }

    /// <summary>
    /// Creates a new connection.
    /// </summary>
    [HttpPost]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateConnection(
        CreateConnectionRequestModel requestModel,
        CancellationToken cancellationToken = default)
    {
        var connection = _mapper.Map<Core.Connections.Connection>(requestModel)!;

        var created = await _connectionService.CreateConnectionAsync(connection, cancellationToken: cancellationToken);

        return CreatedAtAction(
            nameof(ByIdConnectionController.GetConnectionById),
            nameof(ByIdConnectionController).Replace("Controller", string.Empty),
            new { id = created.Id },
            created.Id.ToString());
    }
}
