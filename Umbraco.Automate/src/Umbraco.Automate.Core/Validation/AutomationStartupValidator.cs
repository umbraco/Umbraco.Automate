using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.ControlFlow;
using Umbraco.Automate.Core.Triggers;

namespace Umbraco.Automate.Core.Validation;

/// <summary>
/// Background service that validates all published automations on startup,
/// checking that their trigger and step action aliases resolve to registered types.
/// </summary>
internal sealed class AutomationStartupValidator : BackgroundService
{
    private readonly AutomateReadinessSignal _readinessSignal;
    private readonly IAutomationService _automationService;
    private readonly ActionCollection _actions;
    private readonly TriggerCollection _triggers;
    private readonly ControlFlowCollection _controlFlows;
    private readonly ILogger<AutomationStartupValidator> _logger;

    public AutomationStartupValidator(
        AutomateReadinessSignal readinessSignal,
        IAutomationService automationService,
        ActionCollection actions,
        TriggerCollection triggers,
        ControlFlowCollection controlFlows,
        ILogger<AutomationStartupValidator> logger)
    {
        _readinessSignal = readinessSignal;
        _automationService = automationService;
        _actions = actions;
        _triggers = triggers;
        _controlFlows = controlFlows;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!await _readinessSignal.WaitUntilReadyAsync(stoppingToken))
        {
            _logger.LogError(
                "Automate startup migrations failed; startup automation validation was skipped. " +
                "Resolve the migration failure and restart.");
            return;
        }

        try
        {
            var automations = await _automationService.GetAllAutomationsAsync(stoppingToken);
            var active = automations
                .Where(a => a.Status == AutomationStatus.Published)
                .ToList();

            if (active.Count == 0)
            {
                _logger.LogDebug("No published automations to validate");
                return;
            }

            _logger.LogInformation("Validating {Count} published automations", active.Count);

            var invalidCount = 0;
            foreach (var automation in active)
            {
                var issues = ValidateAutomation(automation);
                if (issues.Count > 0)
                {
                    invalidCount++;
                    foreach (var issue in issues)
                    {
                        _logger.LogWarning(
                            "Automation '{Name}' ({Id}): {Issue}",
                            automation.Name,
                            automation.Id,
                            issue);
                    }
                }
            }

            if (invalidCount > 0)
            {
                _logger.LogWarning(
                    "Startup validation found issues in {InvalidCount} of {Total} active automations",
                    invalidCount,
                    active.Count);
            }
            else
            {
                _logger.LogInformation(
                    "All {Count} active automations validated successfully",
                    active.Count);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown requested during validation — expected.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Startup automation validation failed unexpectedly");
        }
    }

    private List<string> ValidateAutomation(Automation automation)
    {
        var issues = new List<string>();

        // Validate trigger alias
        if (automation.Trigger is not null)
        {
            if (_triggers.GetByAlias(automation.Trigger.TriggerAlias) is null)
            {
                issues.Add($"Trigger alias '{automation.Trigger.TriggerAlias}' is not registered");
            }
        }

        // Validate step action aliases
        foreach (var step in automation.Steps)
        {
            if (_actions.GetByAlias(step.ActionAlias) is not null)
            {
                continue;
            }

            if (_controlFlows.GetByAlias(step.ActionAlias) is not null)
            {
                continue;
            }

            issues.Add($"Step '{step.Name}' references unregistered alias '{step.ActionAlias}'");
        }

        return issues;
    }
}
