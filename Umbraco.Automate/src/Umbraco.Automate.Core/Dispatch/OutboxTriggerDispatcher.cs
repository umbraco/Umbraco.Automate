using System.Text.Json;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Messaging;
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
    private readonly ILogger<OutboxTriggerDispatcher> _logger;

    public OutboxTriggerDispatcher(IOutbox outbox, ILogger<OutboxTriggerDispatcher> logger)
    {
        _outbox = outbox;
        _logger = logger;
    }

    public async Task DispatchAsync(TriggerEvent triggerEvent, CancellationToken cancellationToken)
    {
        var message = new TriggerEventMessage
        {
            TriggerAlias = triggerEvent.TriggerAlias,
            InitiatorType = triggerEvent.InitiatorType,
            InitiatorId = triggerEvent.InitiatorId,
        };

        // Extract output data from typed trigger events.
        if (triggerEvent is ITriggerEventWithOutput withOutput)
        {
            message.OutputData = JsonSerializer.Serialize(withOutput.GetOutput(), JsonOptions.Default);
            message.OutputTypeName = withOutput.GetOutput().GetType().AssemblyQualifiedName;
        }

        _logger.LogDebug("Dispatching trigger event for {TriggerAlias}", triggerEvent.TriggerAlias);

        await _outbox.PublishAsync(TopicName, message, cancellationToken);
    }
}
