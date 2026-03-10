using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Automate.Core.Messaging;

/// <summary>
/// Background service that polls the outbox for pending messages and dispatches them
/// to registered <see cref="IMessageHandler"/> implementations.
/// </summary>
internal sealed class OutboxDispatcher : BackgroundService
{
    private readonly IOutboxStore _store;
    private readonly IEnumerable<IMessageHandler> _handlers;
    private readonly IRuntimeState _runtimeState;
    private readonly IOptions<OutboxOptions> _options;
    private readonly ILogger<OutboxDispatcher> _logger;
    private readonly string _instanceId = Guid.NewGuid().ToString("N")[..12];

    public OutboxDispatcher(
        IOutboxStore store,
        IEnumerable<IMessageHandler> handlers,
        IRuntimeState runtimeState,
        IOptions<OutboxOptions> options,
        ILogger<OutboxDispatcher> logger)
    {
        _store = store;
        _handlers = handlers;
        _runtimeState = runtimeState;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for Umbraco to finish booting (migrations must complete first).
        while (_runtimeState.Level != RuntimeLevel.Run && !stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }

        var handlersByTopic = _handlers.ToDictionary(h => h.Topic, h => h);
        var topics = handlersByTopic.Keys.ToList();

        if (topics.Count == 0)
        {
            _logger.LogWarning("No message handlers registered, outbox dispatcher shutting down");
            return;
        }

        _logger.LogInformation("Outbox dispatcher started (instance {InstanceId}, topics: {Topics})",
            _instanceId, string.Join(", ", topics));

        var options = _options.Value;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var message = await _store.ClaimNextAsync(topics, _instanceId, stoppingToken);

                if (message is null)
                {
                    await Task.Delay(options.PollInterval, stoppingToken);
                    continue;
                }

                await DispatchMessageAsync(message, handlersByTopic, options, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error in outbox dispatch loop, retrying");
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }
    }

    private async Task DispatchMessageAsync(
        OutboxMessage message,
        Dictionary<string, IMessageHandler> handlersByTopic,
        OutboxOptions options,
        CancellationToken stoppingToken)
    {
        if (!handlersByTopic.TryGetValue(message.Topic, out var handler))
        {
            _logger.LogWarning("No handler for topic {Topic}, dead-lettering message {MessageId}",
                message.Topic, message.Id);
            await _store.MarkDeadLetteredAsync(message.Id, $"No handler for topic '{message.Topic}'", stoppingToken);
            return;
        }

        try
        {
            await handler.HandleAsync(message.Body, stoppingToken);
            await _store.MarkCompletedAsync(message.Id, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Handler failed for message {MessageId} on topic {Topic} (attempt {Attempt})",
                message.Id, message.Topic, message.RetryCount + 1);

            if (message.RetryCount + 1 >= options.MaxRetries)
            {
                _logger.LogError("Message {MessageId} exhausted retries, dead-lettering", message.Id);
                await _store.MarkDeadLetteredAsync(message.Id, ex.Message, stoppingToken);
            }
            else
            {
                var delay = options.BaseRetryDelay * Math.Pow(2, message.RetryCount);
                var nextRetry = DateTime.UtcNow.Add(delay);
                await _store.MarkFailedAsync(message.Id, ex.Message, nextRetry, stoppingToken);
            }
        }
    }
}
