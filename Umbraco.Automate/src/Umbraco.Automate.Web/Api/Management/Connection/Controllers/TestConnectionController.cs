using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Web.Api.Management.Connection.Models;
using Umbraco.Cms.Core.Security;

namespace Umbraco.Automate.Web.Api.Management.Connection.Controllers;

/// <summary>
/// Runs the connection type's connectivity check against a saved connection.
/// </summary>
[ApiVersion("1.0")]
public sealed class TestConnectionController : ConnectionControllerBase
{
    private readonly IConnectionService _connectionService;
    private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestConnectionController"/> class.
    /// </summary>
    public TestConnectionController(
        IConnectionService connectionService,
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor)
    {
        _connectionService = connectionService;
        _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
    }

    /// <summary>
    /// Tests a connection by invoking its type's connectivity check.
    /// </summary>
    [HttpPost("{id:guid}/test")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(ConnectionValidationResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TestConnection(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var adminRequired = RequireAdmin(_backOfficeSecurityAccessor);
        if (adminRequired is not null)
        {
            return adminRequired;
        }

        var result = await _connectionService.TestConnectionAsync(id, cancellationToken);
        if (result is null)
        {
            return ConnectionNotFound();
        }

        return Ok(new ConnectionValidationResponseModel
        {
            Status = result.Status switch
            {
                ConnectionValidationStatus.Success => ConnectionValidationStatusModel.Success,
                ConnectionValidationStatus.Warning => ConnectionValidationStatusModel.Warning,
                _ => ConnectionValidationStatusModel.Failure,
            },
            Message = result.Message,
            Details = result.Details,
        });
    }
}
