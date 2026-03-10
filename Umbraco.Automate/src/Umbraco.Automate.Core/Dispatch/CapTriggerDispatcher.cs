using System.Text.Json;
using DotNetCore.CAP;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Triggers;

namespace Umbraco.Automate.Core.Dispatch;

/// <summary>
/// Default <see cref="ITriggerDispatcher"/> implementation that publishes trigger events
/// via CAP for transactional outbox guarantees and fan-out to matching automations.
/// </summary>
internal sealed class CapTriggerDispatcher : ITriggerDispatcher
{
    internal const string TopicName = "umbraco.automate.trigger";

    private readonly ICapPublisher _capPublisher;
    private readonly ILogger<CapTriggerDispatcher> _logger;

    public CapTriggerDispatcher(ICapPublisher capPublisher, ILogger<CapTriggerDispatcher> logger)
    {
        _capPublisher = capPublisher;
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

        await _capPublisher.PublishAsync(TopicName, message, cancellationToken: cancellationToken);
    }
}
