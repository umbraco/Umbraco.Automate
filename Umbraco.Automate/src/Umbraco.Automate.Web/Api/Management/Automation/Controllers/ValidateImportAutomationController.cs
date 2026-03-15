using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Automations.Transfer;
using Umbraco.Automate.Web.Api.Management.Automation.Models;

namespace Umbraco.Automate.Web.Api.Management.Automation.Controllers;

/// <summary>
/// Validates an automation import without creating anything (dry-run).
/// </summary>
[ApiVersion("1.0")]
public sealed class ValidateImportAutomationController : AutomationControllerBase
{
    private readonly IAutomationService _automationService;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateImportAutomationController"/> class.
    /// </summary>
    public ValidateImportAutomationController(
        IAutomationService automationService,
        IAuthorizationService authorizationService)
    {
        _automationService = automationService;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Validates an automation import against the target environment without creating anything.
    /// Returns validation errors and warnings about post-import configuration.
    /// </summary>
    [HttpPost("import/validate")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(AutomationImportResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ValidateImport(
        ImportAutomationRequestModel requestModel,
        CancellationToken cancellationToken = default)
    {
        var forbidden = await AuthorizeWorkspaceAccessAsync(_authorizationService, requestModel.WorkspaceId);
        if (forbidden is not null)
        {
            return forbidden;
        }

        var result = await _automationService.ValidateImportAsync(
            requestModel.ExportModel,
            requestModel.WorkspaceId,
            cancellationToken);

        return Ok(result);
    }
}
