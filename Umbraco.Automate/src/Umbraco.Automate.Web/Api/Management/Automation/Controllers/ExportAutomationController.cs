using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Automations.Transfer;

namespace Umbraco.Automate.Web.Api.Management.Automation.Controllers;

/// <summary>
/// Exports an automation as a portable JSON definition.
/// </summary>
[ApiVersion("1.0")]
public sealed class ExportAutomationController : AutomationControllerBase
{
    private readonly IAutomationService _automationService;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExportAutomationController"/> class.
    /// </summary>
    public ExportAutomationController(
        IAutomationService automationService,
        IAuthorizationService authorizationService)
    {
        _automationService = automationService;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Exports an automation as a portable JSON definition.
    /// Optional sections can be included via the <c>include</c> query parameter.
    /// </summary>
    /// <param name="id">The automation ID.</param>
    /// <param name="include">Comma-separated list of optional sections to include (e.g. "notifications").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("{id:guid}/export")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(AutomationExportModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportAutomation(
        Guid id,
        [FromQuery] string? include = null,
        CancellationToken cancellationToken = default)
    {
        var automation = await _automationService.GetAutomationAsync(id, cancellationToken);
        if (automation is null)
        {
            return AutomationNotFound();
        }

        var forbidden = await AuthorizeWorkspaceAccessAsync(_authorizationService, automation.WorkspaceId);
        if (forbidden is not null)
        {
            return forbidden;
        }

        var includeSections = ParseIncludeSections(include);

        var options = new AutomationExportOptions
        {
            IncludeNotifications = includeSections.Contains("notifications"),
        };

        var exportModel = await _automationService.ExportAutomationAsync(id, options, cancellationToken);
        if (exportModel is null)
        {
            return AutomationNotFound();
        }

        return Ok(exportModel);
    }

    private static HashSet<string> ParseIncludeSections(string? include)
    {
        if (string.IsNullOrWhiteSpace(include))
        {
            return [];
        }

        return include
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
