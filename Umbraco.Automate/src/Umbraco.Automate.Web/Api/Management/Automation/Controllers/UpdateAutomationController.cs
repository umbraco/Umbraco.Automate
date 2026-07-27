using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Web.Api.Management.Automation.Models;
using Umbraco.Cms.Core.Mapping;
using Umbraco.Cms.Core.Security;

namespace Umbraco.Automate.Web.Api.Management.Automation.Controllers;

/// <summary>
/// Updates an existing automation.
/// </summary>
[ApiVersion("1.0")]
public sealed class UpdateAutomationController : AutomationControllerBase
{
    private readonly IAutomationService _automationService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;
    private readonly IUmbracoMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateAutomationController"/> class.
    /// </summary>
    public UpdateAutomationController(
        IAutomationService automationService,
        IAuthorizationService authorizationService,
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
        IUmbracoMapper mapper)
    {
        _automationService = automationService;
        _authorizationService = authorizationService;
        _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
        _mapper = mapper;
    }

    /// <summary>
    /// Updates an existing automation.
    /// </summary>
    [HttpPut("{id:guid}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateAutomation(
        Guid id,
        UpdateAutomationRequestModel requestModel,
        CancellationToken cancellationToken = default)
    {
        var existing = await _automationService.GetAutomationAsync(id, cancellationToken);
        if (existing is null)
        {
            return AutomationNotFound();
        }

        var forbidden = await AuthorizeWorkspaceAccessAsync(_authorizationService, existing.WorkspaceId);
        if (forbidden is not null)
        {
            return forbidden;
        }

        if (requestModel.Version != existing.Version)
        {
            return ConcurrencyConflict("automation");
        }

        _mapper.Map(requestModel, existing);

        try
        {
            await _automationService.UpdateAutomationAsync(existing, CurrentUserKey(_backOfficeSecurityAccessor), cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return ConcurrencyConflict("automation");
        }
        catch (AutomationValidationException ex)
        {
            return ValidationFailed(ex.Message, ex.Errors);
        }

        return Ok();
    }
}
