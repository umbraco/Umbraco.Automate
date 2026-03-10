using System.Collections.Concurrent;
using DotNetCore.CAP;
using Microsoft.Extensions.Logging;
using WorkflowCore.Interface;

namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// WorkflowCore <see cref="IQueueProvider"/> backed by CAP for distributed step execution.
/// Publishes work items to CAP topics and consumes them via subscriptions.
/// </summary>
internal sealed class CapQueueProvider : IQueueProvider, ICapSubscribe
{
    internal const string WorkflowQueueTopic = "umbraco.automate.workflow.queue";
    internal const string EventQueueTopic = "umbraco.automate.workflow.events";

    private readonly ICapPublisher _capPublisher;
    private readonly ILogger<CapQueueProvider> _logger;
    private readonly ConcurrentDictionary<QueueType, ConcurrentQueue<string>> _localQueues = new();

    private bool _isDisposed;

    public CapQueueProvider(ICapPublisher capPublisher, ILogger<CapQueueProvider> logger)
    {
        _capPublisher = capPublisher;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsDequeueBlocking => false;

    /// <inheritdoc />
    public async Task QueueWork(string id, QueueType queue)
    {
        var topic = queue == QueueType.Workflow ? WorkflowQueueTopic : EventQueueTopic;

        _logger.LogTrace("Queuing work {Id} to {Queue}", id, queue);
        await _capPublisher.PublishAsync(topic, new QueueMessage { Id = id, Queue = (int)queue });
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
    /// CAP subscriber that receives workflow queue messages and adds them to the local queue.
    /// </summary>
    [CapSubscribe(WorkflowQueueTopic)]
    public Task HandleWorkflowQueueAsync(QueueMessage message)
    {
        EnqueueLocally(QueueType.Workflow, message.Id);
        return Task.CompletedTask;
    }

    /// <summary>
    /// CAP subscriber that receives event queue messages and adds them to the local queue.
    /// </summary>
    [CapSubscribe(EventQueueTopic)]
    public Task HandleEventQueueAsync(QueueMessage message)
    {
        EnqueueLocally(QueueType.Event, message.Id);
        return Task.CompletedTask;
    }

    private void EnqueueLocally(QueueType queue, string id)
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
/// Message type for the WorkflowCore queue backed by CAP.
/// </summary>
internal sealed class QueueMessage
{
    public required string Id { get; set; }

    public int Queue { get; set; }
}
