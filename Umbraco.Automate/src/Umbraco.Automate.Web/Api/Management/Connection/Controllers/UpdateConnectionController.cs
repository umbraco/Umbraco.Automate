using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Web.Api.Management.Connection.Models;
using Umbraco.Cms.Core.Mapping;
using Umbraco.Cms.Core.Security;

namespace Umbraco.Automate.Web.Api.Management.Connection.Controllers;

/// <summary>
/// Updates an existing connection.
/// </summary>
[ApiVersion("1.0")]
public sealed class UpdateConnectionController : ConnectionControllerBase
{
    private readonly IConnectionService _connectionService;
    private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;
    private readonly IUmbracoMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateConnectionController"/> class.
    /// </summary>
    public UpdateConnectionController(
        IConnectionService connectionService,
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
        IUmbracoMapper mapper)
    {
        _connectionService = connectionService;
        _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
        _mapper = mapper;
    }

    /// <summary>
    /// Updates an existing connection.
    /// </summary>
    [HttpPut("{id:guid}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateConnection(
        Guid id,
        UpdateConnectionRequestModel requestModel,
        CancellationToken cancellationToken = default)
    {
        var adminRequired = RequireAdmin(_backOfficeSecurityAccessor);
        if (adminRequired is not null)
        {
            return adminRequired;
        }

        var existing = await _connectionService.GetConnectionAsync(id, cancellationToken);
        if (existing is null)
        {
            return ConnectionNotFound();
        }

        if (requestModel.Version != existing.Version)
        {
            return ConcurrencyConflict("connection");
        }

        _mapper.Map(requestModel, existing);

        try
        {
            await _connectionService.UpdateConnectionAsync(existing, CurrentUserKey(_backOfficeSecurityAccessor), cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return ConcurrencyConflict("connection");
        }

        return Ok();
    }
}
