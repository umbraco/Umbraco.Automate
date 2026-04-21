using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Automations.Transfer;

namespace Umbraco.Automate.Web.Api.Management.Automation.Controllers;

/// <summary>
/// Overwrites an existing automation from a portable JSON definition. The export's ID must match
/// the target automation ID; use <see cref="ImportNewAutomationController"/> to create a new
/// automation.
/// </summary>
[ApiVersion("1.0")]
public sealed class ImportExistingAutomationController : AutomationControllerBase
{
    private readonly IAutomationService _automationService;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImportExistingAutomationController"/> class.
    /// </summary>
    public ImportExistingAutomationController(
        IAutomationService automationService,
        IAuthorizationService authorizationService)
    {
        _automationService = automationService;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Overwrites the automation with the given ID using the supplied export. The workspace and
    /// lifecycle state (published/draft, enabled/disabled) are preserved; only the automation's
    /// content is replaced.
    /// </summary>
    [HttpPut("{id:guid}/import")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(AutomationImportResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ImportExistingAutomation(
        Guid id,
        AutomationExportModel exportModel,
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

        var result = await _automationService.ImportAutomationAsync(
            exportModel,
            existing.WorkspaceId,
            existingAutomationId: id,
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

        return Ok(result);
    }
}
