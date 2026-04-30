using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Dispatch;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Cms.Api.Common.Builders;

namespace Umbraco.Automate.Web.Api.Management.Automation.Controllers;

/// <summary>
/// Manually triggers an automation, dispatching a trigger event for immediate execution.
/// </summary>
[ApiVersion("1.0")]
public sealed class TriggerAutomationController : AutomationControllerBase
{
    private readonly IAutomationService _automationService;
    private readonly IAuthorizationService _authorizationService;
    private readonly ITriggerDispatcher _dispatcher;

    /// <summary>
    /// Initializes a new instance of the <see cref="TriggerAutomationController"/> class.
    /// </summary>
    public TriggerAutomationController(
        IAutomationService automationService,
        IAuthorizationService authorizationService,
        ITriggerDispatcher dispatcher)
    {
        _automationService = automationService;
        _authorizationService = authorizationService;
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// Manually triggers an automation. The automation must be published and enabled.
    /// </summary>
    [HttpPost("{id:guid}/trigger")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> TriggerAutomation(
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
                .WithDetail("The automation must be published and enabled to be triggered.")
                .Build());
        }

        // Dispatch using the automation's configured trigger alias so the consumer
        // matches it correctly — or fall back to manual trigger alias.
        var triggerAlias = automation.Trigger?.TriggerAlias ?? "umbracoAutomate.manual";

        await _dispatcher.DispatchAsync(
            new TriggerEvent
            {
                TriggerAlias = triggerAlias,
                InitiatorType = TriggerInitiatorType.User,
            },
            cancellationToken);

        return Accepted();
    }
}
