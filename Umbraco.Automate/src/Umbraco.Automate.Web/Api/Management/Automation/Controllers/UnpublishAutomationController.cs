using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Automations;
using Umbraco.Cms.Core.Security;

namespace Umbraco.Automate.Web.Api.Management.Automation.Controllers;

/// <summary>
/// Unpublishes an automation, setting it to inactive.
/// </summary>
[ApiVersion("1.0")]
public sealed class UnpublishAutomationController : AutomationControllerBase
{
    private readonly IAutomationService _automationService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnpublishAutomationController"/> class.
    /// </summary>
    public UnpublishAutomationController(
        IAutomationService automationService,
        IAuthorizationService authorizationService,
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor)
    {
        _automationService = automationService;
        _authorizationService = authorizationService;
        _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
    }

    /// <summary>
    /// Unpublishes an automation.
    /// </summary>
    [HttpPost("{id:guid}/unpublish")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UnpublishAutomation(
        Guid id,
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

        try
        {
            await _automationService.UnpublishAutomationAsync(id, CurrentUserKey(_backOfficeSecurityAccessor), cancellationToken);
        }
        catch (AutomationValidationException ex)
        {
            return ValidationFailed(ex.Message, ex.Errors);
        }

        return Ok();
    }
}
