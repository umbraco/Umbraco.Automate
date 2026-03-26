using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Connections;
using Umbraco.Cms.Core.Security;

namespace Umbraco.Automate.Web.Api.Management.Connection.Controllers;

/// <summary>
/// Deletes a connection.
/// </summary>
[ApiVersion("1.0")]
public sealed class DeleteConnectionController : ConnectionControllerBase
{
    private readonly IConnectionService _connectionService;
    private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteConnectionController"/> class.
    /// </summary>
    public DeleteConnectionController(
        IConnectionService connectionService,
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor)
    {
        _connectionService = connectionService;
        _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
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
        var adminRequired = RequireAdmin(_backOfficeSecurityAccessor);
        if (adminRequired is not null)
        {
            return adminRequired;
        }

        var deleted = await _connectionService.DeleteConnectionAsync(id, cancellationToken);
        if (!deleted)
        {
            return ConnectionNotFound();
        }

        return Ok();
    }
}
