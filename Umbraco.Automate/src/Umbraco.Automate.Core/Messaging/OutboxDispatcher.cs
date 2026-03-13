using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Diagnostics;

namespace Umbraco.Automate.Core.Messaging;

/// <summary>
/// Background service that polls the outbox for pending messages and dispatches them
/// to registered <see cref="IMessageHandler"/> implementations.
/// </summary>
internal sealed class OutboxDispatcher : BackgroundService
{
    private readonly IOutboxStore _store;
    private readonly IEnumerable<IMessageHandler> _handlers;
    private readonly AutomateReadinessSignal _readinessSignal;
    private readonly IOptions<OutboxOptions> _options;
    private readonly AutomateMetrics _metrics;
    private readonly ILogger<OutboxDispatcher> _logger;
    private readonly string _instanceId = Guid.NewGuid().ToString("N")[..12];

    /// <summary>
    /// Tracks whether a message is currently being dispatched. Used during shutdown
    /// to give the in-flight message a grace period before forceful cancellation.
    /// </summary>
    private Task? _activeDispatch;

    private DateTime _lastDepthUpdate = DateTime.MinValue;
    private static readonly TimeSpan DepthUpdateInterval = TimeSpan.FromSeconds(30);

    public OutboxDispatcher(
        IOutboxStore store,
        IEnumerable<IMessageHandler> handlers,
        AutomateReadinessSignal readinessSignal,
        IOptions<OutboxOptions> options,
        AutomateMetrics metrics,
        ILogger<OutboxDispatcher> logger)
    {
        _store = store;
        _handlers = handlers;
        _readinessSignal = readinessSignal;
        _options = options;
        _metrics = metrics;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for Automate migrations to complete before accessing the database.
        await _readinessSignal.WaitAsync(stoppingToken);

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

        // Drain token: used to give the in-flight message a grace period on shutdown.
        // stoppingToken stops the polling loop; drainCts cancels after DrainTimeout.
        using var drainCts = new CancellationTokenSource();

        // Suppress execution context flow so that Umbraco's ambient scope (AsyncLocal)
        // from the hosting thread does not leak into this background loop. Without this,
        // scopes created by IOutboxStore clash with the ambient scope from the request
        // pipeline, causing "Scope being disposed is not the Ambient Scope" errors.
        using var _ = ExecutionContext.SuppressFlow();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await UpdateOutboxDepthAsync(stoppingToken);

                var message = await _store.ClaimNextAsync(topics, _instanceId, stoppingToken);

                if (message is null)
                {
                    await Task.Delay(options.PollInterval, stoppingToken);
                    continue;
                }

                _activeDispatch = DispatchMessageAsync(message, handlersByTopic, options, drainCts.Token);
                await _activeDispatch;
                _activeDispatch = null;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error in outbox dispatch loop, retrying");
                _activeDispatch = null;

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        // Graceful drain: if a message is in-flight, wait up to DrainTimeout for it to finish.
        if (_activeDispatch is not null)
        {
            _logger.LogInformation(
                "Outbox dispatcher draining — waiting up to {DrainTimeout} for in-flight message",
                options.DrainTimeout);

            var completed = await Task.WhenAny(_activeDispatch, Task.Delay(options.DrainTimeout));

            if (completed != _activeDispatch)
            {
                _logger.LogWarning("Drain timeout exceeded, cancelling in-flight message");
                await drainCts.CancelAsync();
            }
        }

        _logger.LogInformation("Outbox dispatcher stopped (instance {InstanceId})", _instanceId);
    }

    private async Task DispatchMessageAsync(
        OutboxMessage message,
        Dictionary<string, IMessageHandler> handlersByTopic,
        OutboxOptions options,
        CancellationToken cancellationToken)
    {
        if (!handlersByTopic.TryGetValue(message.Topic, out var handler))
        {
            _logger.LogWarning("No handler for topic {Topic}, dead-lettering message {MessageId}",
                message.Topic, message.Id);
            await _store.MarkDeadLetteredAsync(message.Id, $"No handler for topic '{message.Topic}'", cancellationToken);
            return;
        }

        try
        {
            await handler.HandleAsync(message.Body, cancellationToken);
            await _store.MarkCompletedAsync(message.Id, cancellationToken);
            _metrics.OutboxMessageCompleted(message.Topic);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Handler failed for message {MessageId} on topic {Topic} (attempt {Attempt})",
                message.Id, message.Topic, message.RetryCount + 1);

            if (message.RetryCount + 1 >= options.MaxRetries)
            {
                _logger.LogError("Message {MessageId} exhausted retries, dead-lettering", message.Id);
                await _store.MarkDeadLetteredAsync(message.Id, ex.Message, cancellationToken);
                _metrics.OutboxMessageDeadLettered(message.Topic);
            }
            else
            {
                var delay = options.BaseRetryDelay * Math.Pow(2, message.RetryCount);
                var nextRetry = DateTime.UtcNow.Add(delay);
                await _store.MarkFailedAsync(message.Id, ex.Message, nextRetry, cancellationToken);
            }
        }
    }

    private async Task UpdateOutboxDepthAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        if (now - _lastDepthUpdate < DepthUpdateInterval)
        {
            return;
        }

        try
        {
            var stats = await _store.GetStatsAsync(cancellationToken);
            _metrics.UpdateOutboxDepth(stats.Pending, stats.Processing);
            _lastDepthUpdate = now;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to update outbox depth metrics");
        }
    }
}
