using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Automations.Transfer;
using Umbraco.Automate.Web.Api.Management.Automation.Models;

namespace Umbraco.Automate.Web.Api.Management.Automation.Controllers;

/// <summary>
/// Imports a new automation from a portable JSON definition. Fails if the export's ID or alias
/// already exists in the target environment — use <see cref="ImportExistingAutomationController"/>
/// to overwrite an existing automation.
/// </summary>
[ApiVersion("1.0")]
public sealed class ImportNewAutomationController : AutomationControllerBase
{
    private readonly IAutomationService _automationService;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImportNewAutomationController"/> class.
    /// </summary>
    public ImportNewAutomationController(
        IAutomationService automationService,
        IAuthorizationService authorizationService)
    {
        _automationService = automationService;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Imports a new automation into the target workspace. The automation is created as Draft and disabled,
    /// preserving the ID from the export so references remain stable across environments.
    /// </summary>
    [HttpPost("import")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(AutomationImportResult), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ImportNewAutomation(
        ImportAutomationRequestModel requestModel,
        CancellationToken cancellationToken = default)
    {
        var forbidden = await AuthorizeWorkspaceAccessAsync(_authorizationService, requestModel.WorkspaceId);
        if (forbidden is not null)
        {
            return forbidden;
        }

        var result = await _automationService.ImportAutomationAsync(
            requestModel.ExportModel,
            requestModel.WorkspaceId,
            cancellationToken: cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Import failed",
                Detail = string.Join(" ", result.Errors),
                Extensions = { ["errors"] = result.Errors },
            });
        }

        return CreatedAtAction(
            nameof(ByIdAutomationController.GetAutomationById),
            nameof(ByIdAutomationController).Replace("Controller", string.Empty),
            new { id = result.AutomationId },
            result);
    }
}
