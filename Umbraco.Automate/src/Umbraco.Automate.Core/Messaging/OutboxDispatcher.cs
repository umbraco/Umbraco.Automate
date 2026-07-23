using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Diagnostics;
using Umbraco.Automate.Core.Execution;

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
    private readonly IExecutionNodeEligibility _eligibility;
    private readonly ILogger<OutboxDispatcher> _logger;
    private readonly string _instanceId = Guid.NewGuid().ToString("N")[..12];

    /// <summary>
    /// Tracks whether a message is currently being dispatched. Used during shutdown
    /// to give the in-flight message a grace period before forceful cancellation.
    /// </summary>
    private Task? _activeDispatch;

    private DateTime _lastDepthUpdate = DateTime.MinValue;
    private int _lastKnownPending;
    private static readonly TimeSpan DepthUpdateInterval = TimeSpan.FromSeconds(30);

    /// <summary>
    /// When the current "messages pending but nothing eligible to claim them" condition
    /// first began, and whether we have already warned about it. Used to warn exactly once
    /// per stall (edge-triggered), and only after the stall has persisted past
    /// <see cref="StallWarningThreshold"/> so we don't false-alarm during the brief startup
    /// window where the server role is legitimately still settling.
    /// </summary>
    private DateTime? _stallSince;
    private bool _stallWarned;
    private static readonly TimeSpan StallWarningThreshold = TimeSpan.FromSeconds(60);

    public OutboxDispatcher(
        IOutboxStore store,
        IEnumerable<IMessageHandler> handlers,
        AutomateReadinessSignal readinessSignal,
        IOptions<OutboxOptions> options,
        AutomateMetrics metrics,
        IExecutionNodeEligibility eligibility,
        ILogger<OutboxDispatcher> logger)
    {
        _store = store;
        _handlers = handlers;
        _readinessSignal = readinessSignal;
        _options = options;
        _metrics = metrics;
        _eligibility = eligibility;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for Automate migrations to complete before accessing the database.
        if (!await _readinessSignal.WaitUntilReadyAsync(stoppingToken))
        {
            _logger.LogError(
                "Automate startup migrations failed; the outbox dispatcher will not start. " +
                "Resolve the migration failure and restart.");
            return;
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

        // Drain token: used to give the in-flight message a grace period on shutdown.
        // stoppingToken stops the polling loop; drainCts cancels after DrainTimeout.
        using var drainCts = new CancellationTokenSource();

        // Suppress execution context flow so that Umbraco's ambient scope (AsyncLocal)
        // from the hosting thread does not leak into this background loop. Without this,
        // scopes created by IOutboxStore clash with the ambient scope from the request
        // pipeline, causing "Scope being disposed is not the Ambient Scope" errors.
        //
        // Pattern: SuppressFlow → start Task.Run (inherits no context) → Undo on same thread.
        // AsyncFlowControl.Undo() must run on the thread that called SuppressFlow(), so we
        // cannot use a long-lived `using` in an async method (the Dispose could run on any thread).
        var flowControl = ExecutionContext.SuppressFlow();
        var loopTask = Task.Run(() => RunPollLoopAsync(handlersByTopic, options, drainCts, stoppingToken));
        flowControl.Undo();

        await loopTask;
    }

    private async Task RunPollLoopAsync(
        Dictionary<string, IMessageHandler> handlersByTopic,
        OutboxOptions options,
        CancellationTokenSource drainCts,
        CancellationToken stoppingToken)
    {
        // Track previous eligibility set so we only log on transitions, not every poll.
        var lastEligibleTopics = new HashSet<string>(StringComparer.Ordinal);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await UpdateOutboxDepthAsync(stoppingToken);

                // Filter topics by handler eligibility each poll. A handler that returns
                // CanProcessNow() == false (e.g. server role is Unknown during startup, or
                // this node is a Subscriber in SchedulerOnly mode) is excluded so its
                // messages remain in Pending for an eligible node to claim.
                var eligibleTopics = handlersByTopic
                    .Where(kvp => kvp.Value.CanProcessNow())
                    .Select(kvp => kvp.Key)
                    .ToList();

                LogEligibilityTransitions(lastEligibleTopics, eligibleTopics, handlersByTopic.Keys);
                WarnIfStalled(eligibleTopics.Count, _lastKnownPending);

                if (eligibleTopics.Count == 0)
                {
                    await Task.Delay(options.PollInterval, stoppingToken);
                    continue;
                }

                var message = await _store.ClaimNextAsync(eligibleTopics, _instanceId, stoppingToken);

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
        catch (NodeNotEligibleException)
        {
            // The handler became ineligible between the claim filter and HandleAsync —
            // release the claim back to Pending without consuming a retry slot, so the
            // message stays available for an eligible node (or this node on a later poll).
            _logger.LogInformation(
                "Releasing message {MessageId} on topic {Topic} — node is no longer eligible to process it",
                message.Id, message.Topic);
            await _store.ReleaseClaimAsync(message.Id, cancellationToken);
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

    private void LogEligibilityTransitions(
        HashSet<string> lastEligibleTopics,
        IEnumerable<string> currentEligibleTopics,
        IEnumerable<string> allTopics)
    {
        var current = new HashSet<string>(currentEligibleTopics, StringComparer.Ordinal);

        // Newly eligible (became processable this iteration)
        foreach (var topic in current)
        {
            if (lastEligibleTopics.Add(topic))
            {
                _logger.LogInformation(
                    "Topic {Topic} is now eligible for processing on this node", topic);
            }
        }

        // Newly ineligible (was eligible last iteration, no longer is)
        foreach (var topic in allTopics)
        {
            if (!current.Contains(topic) && lastEligibleTopics.Remove(topic))
            {
                _logger.LogInformation(
                    "Topic {Topic} is no longer eligible for processing on this node — messages will remain pending",
                    topic);
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
            _lastKnownPending = stats.Pending;
            _lastDepthUpdate = now;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to update outbox depth metrics");
        }
    }

    /// <summary>
    /// Warns (once, edge-triggered) when there are messages waiting but no topic is eligible
    /// for processing on this node — a silent dead-end where work accumulates and nothing
    /// consumes it. The most common cause is a misconfigured single-server install whose
    /// <c>ServerRole</c> never leaves <c>Unknown</c>; the explanation comes from
    /// <see cref="IExecutionNodeEligibility.CanExecuteWorkflows(out string?)"/> so the message
    /// stays in sync with the actual eligibility rule. A <see cref="StallWarningThreshold"/>
    /// grace window avoids false alarms while the role is still settling at startup.
    /// </summary>
    private void WarnIfStalled(int eligibleTopicCount, int pendingDepth)
    {
        var stalled = eligibleTopicCount == 0 && pendingDepth > 0;

        if (!stalled)
        {
            if (_stallWarned)
            {
                _logger.LogInformation(
                    "Outbox processing resumed — this node is eligible to consume messages again");
            }

            _stallSince = null;
            _stallWarned = false;
            return;
        }

        _stallSince ??= DateTime.UtcNow;

        if (_stallWarned || DateTime.UtcNow - _stallSince < StallWarningThreshold)
        {
            return;
        }

        _stallWarned = true;
        _eligibility.CanExecuteWorkflows(out var reason);
        _logger.LogWarning(
            "Outbox has {Pending} pending message(s) but no topics are eligible for processing on this node, " +
            "so nothing will be consumed. {Reason}",
            pendingDepth, reason);
    }
}
