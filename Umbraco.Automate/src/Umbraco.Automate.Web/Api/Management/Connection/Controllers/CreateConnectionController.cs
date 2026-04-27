using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Web.Api.Management.Connection.Models;
using Umbraco.Cms.Core.Mapping;
using Umbraco.Cms.Core.Security;

namespace Umbraco.Automate.Web.Api.Management.Connection.Controllers;

/// <summary>
/// Creates a new connection.
/// </summary>
[ApiVersion("1.0")]
public sealed class CreateConnectionController : ConnectionControllerBase
{
    private readonly IConnectionService _connectionService;
    private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;
    private readonly IUmbracoMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateConnectionController"/> class.
    /// </summary>
    public CreateConnectionController(
        IConnectionService connectionService,
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
        IUmbracoMapper mapper)
    {
        _connectionService = connectionService;
        _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
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
        var adminRequired = RequireAdmin(_backOfficeSecurityAccessor);
        if (adminRequired is not null)
        {
            return adminRequired;
        }

        var connection = _mapper.Map<Core.Connections.Connection>(requestModel)!;

        var created = await _connectionService.CreateConnectionAsync(connection, CurrentUserKey(_backOfficeSecurityAccessor), cancellationToken);

        return CreatedAtAction(
            nameof(ByIdConnectionController.GetConnectionById),
            nameof(ByIdConnectionController).Replace("Controller", string.Empty),
            new { id = created.Id },
            created.Id.ToString());
    }
}
