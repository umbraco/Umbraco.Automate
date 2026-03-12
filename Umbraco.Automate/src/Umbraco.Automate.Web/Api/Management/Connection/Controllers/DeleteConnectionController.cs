using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Connections;

namespace Umbraco.Automate.Web.Api.Management.Connection.Controllers;

/// <summary>
/// Deletes a connection.
/// </summary>
[ApiVersion("1.0")]
public sealed class DeleteConnectionController : ConnectionControllerBase
{
    private readonly IConnectionService _connectionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteConnectionController"/> class.
    /// </summary>
    public DeleteConnectionController(IConnectionService connectionService)
    {
        _connectionService = connectionService;
    }

    /// <summary>
    /// Deletes a connection.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteConnection(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var deleted = await _connectionService.DeleteConnectionAsync(id, cancellationToken);
        if (!deleted)
        {
            return ConnectionNotFound();
        }

        return Ok();
    }
}
