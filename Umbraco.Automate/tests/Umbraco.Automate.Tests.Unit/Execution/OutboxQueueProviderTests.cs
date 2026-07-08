using Microsoft.Extensions.Logging.Abstractions;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Messaging;
using WorkflowCore.Interface;

namespace Umbraco.Automate.Tests.Unit.Execution;

/// <summary>
/// Regression coverage for the QueueType.Index misrouting bug: WorkflowConsumer unconditionally
/// queues QueueType.Index after every processing pass, and this provider must not publish it —
/// falling through to the Event topic makes EventConsumer treat a workflow instance id as an
/// event id, failing with "Event '{id}' not found" on every single pass.
/// </summary>
public class OutboxQueueProviderTests
{
    [Fact]
    public async Task QueueWork_WithIndexType_DoesNotPublishToOutbox()
    {
        var outbox = new Mock<IOutbox>();
        var provider = new OutboxQueueProvider(outbox.Object, NullLogger<OutboxQueueProvider>.Instance);

        await provider.QueueWork("some-workflow-instance-id", QueueType.Index);

        outbox.Verify(
            o => o.PublishAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public async Task QueueWork_WithWorkflowType_PublishesToWorkflowTopic()
    {
        var outbox = new Mock<IOutbox>();
        var provider = new OutboxQueueProvider(outbox.Object, NullLogger<OutboxQueueProvider>.Instance);

        await provider.QueueWork("some-workflow-instance-id", QueueType.Workflow);

        outbox.Verify(
            o => o.PublishAsync(
                OutboxQueueProvider.WorkflowQueueTopic, It.IsAny<object>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task QueueWork_WithEventType_PublishesToEventTopic()
    {
        var outbox = new Mock<IOutbox>();
        var provider = new OutboxQueueProvider(outbox.Object, NullLogger<OutboxQueueProvider>.Instance);

        await provider.QueueWork("some-event-id", QueueType.Event);

        outbox.Verify(
            o => o.PublishAsync(
                OutboxQueueProvider.EventQueueTopic, It.IsAny<object>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()),
            Times.Once);
    }
}
