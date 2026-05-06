using System.Text.Json;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Messaging;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Versioning;

namespace Umbraco.Automate.Core.Dispatch;

/// <summary>
/// Handles trigger event messages from the outbox and starts matching automation runs.
/// </summary>
internal sealed class TriggerEventHandler : IMessageHandler
{
    private readonly IAutomationService _automationService;
    private readonly IEntityVersionService _versionService;
    private readonly IAutomationExecutor _executor;
    private readonly IExecutionNodeEligibility _nodeEligibility;
    private readonly TriggerCollection _triggers;
    private readonly ILogger<TriggerEventHandler> _logger;

    public TriggerEventHandler(
        IAutomationService automationService,
        IEntityVersionService versionService,
        IAutomationExecutor executor,
        IExecutionNodeEligibility nodeEligibility,
        TriggerCollection triggers,
        ILogger<TriggerEventHandler> logger)
    {
        _automationService = automationService;
        _versionService = versionService;
        _executor = executor;
        _nodeEligibility = nodeEligibility;
        _triggers = triggers;
        _logger = logger;
    }

    public string Topic => OutboxTriggerDispatcher.TopicName;

    public bool CanProcessNow() => _nodeEligibility.CanExecuteWorkflows();

    public async Task HandleAsync(string body, CancellationToken cancellationToken)
    {
        // Defensive: if eligibility flipped between the dispatcher's pre-claim check and
        // now, throw so the dispatcher retries on backoff (rather than silently completing
        // the message and losing the trigger event).
        if (!_nodeEligibility.CanExecuteWorkflows())
        {
            throw new NodeNotEligibleException(Topic);
        }

        var message = JsonSerializer.Deserialize<TriggerEventMessage>(body, JsonOptions.Default)
                      ?? throw new InvalidOperationException("Failed to deserialize TriggerEventMessage");

        _logger.LogDebug("Received trigger event for {TriggerAlias}", message.TriggerAlias);

        // Find all published automations that use this trigger.
        var automations = await _automationService.GetAllAutomationsAsync(cancellationToken);
        var matching = automations
            .Where(a => a.Status == AutomationStatus.Published
                        && a.Trigger?.TriggerAlias == message.TriggerAlias)
            .ToList();

        if (matching.Count == 0)
        {
            _logger.LogInformation(
                "No published automations matched trigger {TriggerAlias} — event dropped",
                message.TriggerAlias);
            return;
        }

        // Deserialize trigger output data for the run context.
        // Unwrap JsonElement values to primitives so they survive the Newtonsoft.Json
        // round-trip used by the WorkflowCore persistence provider.
        Dictionary<string, object?>? triggerOutputData = null;
        if (!string.IsNullOrEmpty(message.OutputData))
        {
            triggerOutputData = JsonOptions.DeserializeToUnwrappedDictionary(message.OutputData);
        }

        // Look up the trigger so we can consult its per-automation settings filter
        // (ITrigger.CanHandle). Not finding the trigger is unusual but shouldn't drop
        // events — fall through and dispatch unconditionally.
        var trigger = _triggers.GetByAlias(message.TriggerAlias);

        // Typed output is only needed when at least one automation has configured
        // trigger settings. Deserialize lazily to avoid the cost on the common no-filter path.
        object? typedOutput = null;
        var typedOutputAttempted = false;

        foreach (var automation in matching)
        {
            // Resolve the published version snapshot for execution.
            // This ensures we run the frozen, published state — not the current draft.
            var executionAutomation = automation;
            if (automation.PublishedVersion.HasValue)
            {
                var snapshot = await _versionService.GetVersionSnapshotAsync<Automation>(
                    automation.Id, automation.PublishedVersion.Value, cancellationToken);

                if (snapshot is not null)
                {
                    executionAutomation = snapshot;
                }
                else
                {
                    _logger.LogWarning(
                        "Published version {Version} snapshot not found for automation {AutomationId}, using current state",
                        automation.PublishedVersion.Value, automation.Id);
                }
            }

            // Filter: apply the trigger's per-automation settings predicate against the
            // published snapshot's settings. Skip when there are no configured settings,
            // no trigger, or the trigger declares no settings type — nothing to match on.
            var triggerSettings = executionAutomation.Trigger?.Settings;
            if (trigger is not null
                && trigger.SettingsType is not null
                && triggerSettings is { Count: > 0 })
            {
                if (!typedOutputAttempted)
                {
                    typedOutput = DeserializeTypedOutput(message.OutputData, trigger.OutputType);
                    typedOutputAttempted = true;
                }

                if (typedOutput is not null)
                {
                    var resolvedSettings = trigger.ResolveSettings(triggerSettings);
                    if (!trigger.CanHandle(typedOutput, resolvedSettings))
                    {
                        _logger.LogDebug(
                            "Automation {AutomationId} skipped by trigger {TriggerAlias} settings filter",
                            executionAutomation.Id, message.TriggerAlias);
                        continue;
                    }
                }
            }

            _logger.LogInformation(
                "Starting run for automation {AutomationAlias} ({AutomationId}) version {Version} from trigger {TriggerAlias}",
                executionAutomation.Alias, executionAutomation.Id, executionAutomation.Version, message.TriggerAlias);

            await _executor.ExecuteAsync(
                executionAutomation,
                message.InitiatorType,
                message.InitiatorId,
                triggerOutputData,
                cancellationToken);
        }
    }

    private object? DeserializeTypedOutput(string? outputData, Type? outputType)
    {
        if (string.IsNullOrEmpty(outputData) || outputType is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(outputData, outputType, JsonOptions.Default);
        }
        catch (JsonException ex)
        {
            // A deserialization failure shouldn't silently suppress automations. Log and
            // fall through — automations with settings filters will behave as if no filter
            // was configured rather than being dropped.
            _logger.LogWarning(ex,
                "Failed to deserialize trigger output as {OutputType} for filtering — automations with configured settings will fire unfiltered",
                outputType.FullName);
            return null;
        }
    }
}
