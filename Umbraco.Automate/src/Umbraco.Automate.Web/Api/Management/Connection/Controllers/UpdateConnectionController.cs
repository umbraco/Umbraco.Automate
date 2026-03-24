using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Web.Api.Management.Connection.Models;
using Umbraco.Cms.Core.Mapping;

namespace Umbraco.Automate.Web.Api.Management.Connection.Controllers;

/// <summary>
/// Updates an existing connection.
/// </summary>
[ApiVersion("1.0")]
public sealed class UpdateConnectionController : ConnectionControllerBase
{
    private readonly IConnectionService _connectionService;
    private readonly IUmbracoMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateConnectionController"/> class.
    /// </summary>
    public UpdateConnectionController(IConnectionService connectionService, IUmbracoMapper mapper)
    {
        _connectionService = connectionService;
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
        var existing = await _connectionService.GetConnectionAsync(id, cancellationToken);
        if (existing is null)
        {
            return ConnectionNotFound();
        }

        if (requestModel.Version != existing.Version)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Concurrency conflict",
                Detail = "The connection was modified by another request. Reload and try again.",
                Status = StatusCodes.Status409Conflict,
            });
        }

        _mapper.Map(requestModel, existing);

        try
        {
            await _connectionService.UpdateConnectionAsync(existing, cancellationToken: cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Concurrency conflict",
                Detail = "The connection was modified by another request. Reload and try again.",
                Status = StatusCodes.Status409Conflict,
            });
        }

        return Ok();
    }
}
