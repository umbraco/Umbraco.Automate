using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Dispatch;
using Umbraco.Automate.Core.Messaging;
using WorkflowCore.Interface;

namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// WorkflowCore <see cref="IQueueProvider"/> backed by the outbox for reliable step execution.
/// Publishes work items to the outbox and consumes them via message handlers into local queues.
/// </summary>
internal sealed class OutboxQueueProvider : IQueueProvider
{
    internal const string WorkflowQueueTopic = "umbraco.automate.workflow.queue";
    internal const string EventQueueTopic = "umbraco.automate.workflow.events";

    private readonly IOutbox _outbox;
    private readonly ILogger<OutboxQueueProvider> _logger;
    private readonly ConcurrentDictionary<QueueType, ConcurrentQueue<string>> _localQueues = new();

    private bool _isDisposed;

    public OutboxQueueProvider(IOutbox outbox, ILogger<OutboxQueueProvider> logger)
    {
        _outbox = outbox;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsDequeueBlocking => false;

    /// <inheritdoc />
    public async Task QueueWork(string id, QueueType queue)
    {
        var topic = queue == QueueType.Workflow ? WorkflowQueueTopic : EventQueueTopic;

        _logger.LogTrace("Queuing work {Id} to {Queue}", id, queue);
        await _outbox.PublishAsync(topic, new QueueMessage { Id = id, Queue = (int)queue }, CancellationToken.None);
    }

    /// <inheritdoc />
    public Task<string?> DequeueWork(QueueType queue, CancellationToken cancellationToken)
    {
        var localQueue = _localQueues.GetOrAdd(queue, _ => new ConcurrentQueue<string>());

        if (localQueue.TryDequeue(out var id))
        {
            return Task.FromResult<string?>(id);
        }

        return Task.FromResult<string?>(null);
    }

    /// <summary>
    /// Enqueues a work item into the local queue. Called by the message handlers.
    /// </summary>
    internal void EnqueueLocally(QueueType queue, string id)
    {
        var localQueue = _localQueues.GetOrAdd(queue, _ => new ConcurrentQueue<string>());
        localQueue.Enqueue(id);
    }

    /// <inheritdoc />
    public Task Start() => Task.CompletedTask;

    /// <inheritdoc />
    public Task Stop() => Task.CompletedTask;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _localQueues.Clear();
        _isDisposed = true;
    }
}

/// <summary>
/// Serializable message for the WorkflowCore queue.
/// </summary>
internal sealed class QueueMessage
{
    public required string Id { get; set; }

    public int Queue { get; set; }
}

/// <summary>
/// Handles workflow queue messages from the outbox and feeds them into the local queue.
/// </summary>
internal sealed class WorkflowQueueHandler : IMessageHandler
{
    private readonly OutboxQueueProvider _queueProvider;

    public WorkflowQueueHandler(OutboxQueueProvider queueProvider)
    {
        _queueProvider = queueProvider;
    }

    public string Topic => OutboxQueueProvider.WorkflowQueueTopic;

    public Task HandleAsync(string body, CancellationToken cancellationToken)
    {
        var message = JsonSerializer.Deserialize<QueueMessage>(body, JsonOptions.Default)
                      ?? throw new InvalidOperationException("Failed to deserialize QueueMessage");
        _queueProvider.EnqueueLocally(QueueType.Workflow, message.Id);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Handles workflow event messages from the outbox and feeds them into the local queue.
/// </summary>
internal sealed class EventQueueHandler : IMessageHandler
{
    private readonly OutboxQueueProvider _queueProvider;

    public EventQueueHandler(OutboxQueueProvider queueProvider)
    {
        _queueProvider = queueProvider;
    }

    public string Topic => OutboxQueueProvider.EventQueueTopic;

    public Task HandleAsync(string body, CancellationToken cancellationToken)
    {
        var message = JsonSerializer.Deserialize<QueueMessage>(body, JsonOptions.Default)
                      ?? throw new InvalidOperationException("Failed to deserialize QueueMessage");
        _queueProvider.EnqueueLocally(QueueType.Event, message.Id);
        return Task.CompletedTask;
    }
}
