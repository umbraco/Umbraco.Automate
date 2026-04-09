using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Messaging;
using Umbraco.Automate.Core.Versioning;
using Umbraco.Cms.Core.Sync;

namespace Umbraco.Automate.Core.Dispatch;

/// <summary>
/// Handles trigger event messages from the outbox and starts matching automation runs.
/// </summary>
internal sealed class TriggerEventHandler : IMessageHandler
{
    private readonly IAutomationService _automationService;
    private readonly IEntityVersionService _versionService;
    private readonly IAutomationExecutor _executor;
    private readonly IServerRoleAccessor _serverRoleAccessor;
    private readonly IOptions<ExecutionOptions> _executionOptions;
    private readonly ILogger<TriggerEventHandler> _logger;

    public TriggerEventHandler(
        IAutomationService automationService,
        IEntityVersionService versionService,
        IAutomationExecutor executor,
        IServerRoleAccessor serverRoleAccessor,
        IOptions<ExecutionOptions> executionOptions,
        ILogger<TriggerEventHandler> logger)
    {
        _automationService = automationService;
        _versionService = versionService;
        _executor = executor;
        _serverRoleAccessor = serverRoleAccessor;
        _executionOptions = executionOptions;
        _logger = logger;
    }

    public string Topic => OutboxTriggerDispatcher.TopicName;

    public async Task HandleAsync(string body, CancellationToken cancellationToken)
    {
        var message = JsonSerializer.Deserialize<TriggerEventMessage>(body, JsonOptions.Default)
                      ?? throw new InvalidOperationException("Failed to deserialize TriggerEventMessage");

        if (!ShouldProcessOnThisNode())
        {
            _logger.LogDebug(
                "Skipping trigger event {TriggerAlias} — this node ({ServerRole}) is not the designated executor",
                message.TriggerAlias, _serverRoleAccessor.CurrentServerRole);
            return;
        }

        _logger.LogDebug("Received trigger event for {TriggerAlias}", message.TriggerAlias);

        // Find all published & enabled automations that use this trigger.
        var automations = await _automationService.GetAllAutomationsAsync(cancellationToken);
        var matching = automations
            .Where(a => a is { Status: AutomationStatus.Published, IsEnabled: true }
                        && a.Trigger?.TriggerAlias == message.TriggerAlias)
            .ToList();

        if (matching.Count == 0)
        {
            _logger.LogDebug("No matching automations for trigger {TriggerAlias}", message.TriggerAlias);
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

    private bool ShouldProcessOnThisNode()
    {
        if (_executionOptions.Value.Mode == ExecutionMode.Distributed)
        {
            return true;
        }

        // SchedulerOnly: only process on Single or SchedulingPublisher nodes.
        return _serverRoleAccessor.CurrentServerRole is ServerRole.Single
            or ServerRole.SchedulingPublisher;
    }
}
