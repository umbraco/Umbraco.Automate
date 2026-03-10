using System.Text.Json;
using DotNetCore.CAP;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Execution;

namespace Umbraco.Automate.Core.Dispatch;

/// <summary>
/// CAP consumer that receives trigger events and starts matching automation runs.
/// </summary>
internal sealed class TriggerEventConsumer : ICapSubscribe
{
    private readonly IAutomationService _automationService;
    private readonly IAutomationExecutor _executor;
    private readonly ILogger<TriggerEventConsumer> _logger;

    public TriggerEventConsumer(
        IAutomationService automationService,
        IAutomationExecutor executor,
        ILogger<TriggerEventConsumer> logger)
    {
        _automationService = automationService;
        _executor = executor;
        _logger = logger;
    }

    [CapSubscribe(CapTriggerDispatcher.TopicName)]
    public async Task HandleTriggerEventAsync(TriggerEventMessage message, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Received trigger event for {TriggerAlias}", message.TriggerAlias);

        // Find all published & enabled automations that use this trigger.
        var automations = await _automationService.GetAllAutomationsAsync(cancellationToken);
        var matching = automations
            .Where(a => a.Status == AutomationStatus.Published
                        && a.IsEnabled
                        && a.Trigger?.TriggerAlias == message.TriggerAlias)
            .ToList();

        if (matching.Count == 0)
        {
            _logger.LogDebug("No matching automations for trigger {TriggerAlias}", message.TriggerAlias);
            return;
        }

        // Deserialize trigger output data for the run context.
        Dictionary<string, object?>? triggerOutputData = null;
        if (!string.IsNullOrEmpty(message.OutputData))
        {
            triggerOutputData = JsonSerializer.Deserialize<Dictionary<string, object?>>(
                message.OutputData, JsonOptions.Default);
        }

        foreach (var automation in matching)
        {
            _logger.LogInformation(
                "Starting run for automation {AutomationAlias} ({AutomationId}) from trigger {TriggerAlias}",
                automation.Alias, automation.Id, message.TriggerAlias);

            await _executor.ExecuteAsync(
                automation,
                message.InitiatorType,
                message.InitiatorId,
                triggerOutputData,
                cancellationToken);
        }
    }
}
