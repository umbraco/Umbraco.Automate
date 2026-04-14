using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Execution;
using Umbraco.Cms.Api.Common.Builders;

namespace Umbraco.Automate.Web.Api.Management.Automation.Controllers;

/// <summary>
/// Executes an automation in dry-run mode — steps describe what they would do without
/// producing side effects. The run is recorded normally for inspection.
/// </summary>
[ApiVersion("1.0")]
public sealed class DryRunAutomationController : AutomationControllerBase
{
    private readonly IAutomationService _automationService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IAutomationExecutor _executor;

    /// <summary>
    /// Initializes a new instance of the <see cref="DryRunAutomationController"/> class.
    /// </summary>
    public DryRunAutomationController(
        IAutomationService automationService,
        IAuthorizationService authorizationService,
        IAutomationExecutor executor)
    {
        _automationService = automationService;
        _authorizationService = authorizationService;
        _executor = executor;
    }

    /// <summary>
    /// Executes an automation in dry-run mode. Each step returns what it would do
    /// without producing side effects. The automation must be published and enabled.
    /// </summary>
    [HttpPost("{id:guid}/dry-run")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DryRunAutomation(
        Guid id,
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

        if (automation.Status != AutomationStatus.Published || !automation.IsEnabled)
        {
            return Conflict(new ProblemDetailsBuilder()
                .WithTitle("Automation not active")
                .WithDetail("The automation must be published and enabled for a dry run.")
                .Build());
        }

        var runId = await _executor.ExecuteAsync(
            automation,
            "dry-run",
            null,
            null,
            cancellationToken,
            isDryRun: true);

        return Accepted(new { runId });
    }
}
