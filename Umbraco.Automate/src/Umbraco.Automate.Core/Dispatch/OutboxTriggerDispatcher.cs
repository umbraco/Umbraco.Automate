using System.Text.Json;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Diagnostics;
using Umbraco.Automate.Core.Messaging;
using Umbraco.Automate.Core.Persistence;
using Umbraco.Automate.Core.Triggers;

namespace Umbraco.Automate.Core.Dispatch;

/// <summary>
/// <see cref="ITriggerDispatcher"/> implementation that publishes trigger events
/// to the outbox for reliable async dispatch to matching automations.
/// </summary>
internal sealed class OutboxTriggerDispatcher : ITriggerDispatcher
{
    internal const string TopicName = "umbraco.automate.trigger";

    private readonly IOutbox _outbox;
    private readonly AutomateMetrics _metrics;
    private readonly ILogger<OutboxTriggerDispatcher> _logger;

    public OutboxTriggerDispatcher(IOutbox outbox, AutomateMetrics metrics, ILogger<OutboxTriggerDispatcher> logger)
    {
        _outbox = outbox;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task DispatchAsync(TriggerEvent triggerEvent, CancellationToken cancellationToken)
    {
        var message = new TriggerEventMessage
        {
            TriggerAlias = triggerEvent.TriggerAlias,
            InitiatorType = triggerEvent.InitiatorType,
            InitiatorId = triggerEvent.InitiatorId,
            IdempotencyKey = triggerEvent.IdempotencyKey,
        };

        // Extract output data from typed trigger events.
        if (triggerEvent is ITriggerEventWithOutput withOutput)
        {
            message.OutputData = JsonSerializer.Serialize(withOutput.GetOutput(), JsonOptions.Default);
            message.OutputTypeName = withOutput.GetOutput().GetType().AssemblyQualifiedName;
        }

        _logger.LogDebug("Dispatching trigger event for {TriggerAlias}", triggerEvent.TriggerAlias);

        try
        {
            await _outbox.PublishAsync(TopicName, message, cancellationToken, triggerEvent.IdempotencyKey);
        }
        catch (OutboxBackpressureException)
        {
            _metrics.OutboxBackpressureRejection();
            throw;
        }
        catch (Exception ex) when (DatabaseExceptions.IsMissingTable(ex))
        {
            // Outbox table may not exist yet if migrations haven't completed.
            // Silently drop the trigger event — the system isn't ready to process automations.
            _logger.LogDebug(
                "Outbox table not yet available, dropping trigger event for {TriggerAlias}",
                triggerEvent.TriggerAlias);
            return;
        }

        _metrics.TriggerDispatched(triggerEvent.TriggerAlias);
        _metrics.OutboxMessagePublished(TopicName);
    }

}
