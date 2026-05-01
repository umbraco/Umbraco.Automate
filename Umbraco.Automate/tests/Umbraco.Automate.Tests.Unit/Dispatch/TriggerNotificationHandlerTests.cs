using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Dispatch;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.StepTypes;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Automate.Tests.Unit.Dispatch;

public class TriggerNotificationHandlerTests
{
    private static Mock<IRuntimeState> CreateRunningRuntimeState()
    {
        var runtimeState = new Mock<IRuntimeState>();
        runtimeState.Setup(r => r.Level).Returns(RuntimeLevel.Run);
        return runtimeState;
    }

    private static Mock<ITriggerSubscriptionRegistry> CreateAcceptAllRegistry()
    {
        var registry = new Mock<ITriggerSubscriptionRegistry>();
        registry
            .Setup(r => r.HasSubscribersAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return registry;
    }

    private static IAutomationOriginAccessor CreateEmptyOriginAccessor()
    {
        var accessor = new Mock<IAutomationOriginAccessor>();
        accessor.SetupGet(a => a.Current).Returns((AutomationOrigin?)null);
        return accessor.Object;
    }

    [Fact]
    public async Task HandleAsync_DispatchesAllEventsFromAllTriggers()
    {
        var events = new List<TriggerEvent>();
        var dispatcher = new Mock<ITriggerDispatcher>();
        dispatcher
            .Setup(d => d.DispatchAsync(It.IsAny<TriggerEvent>(), It.IsAny<CancellationToken>()))
            .Callback<TriggerEvent, CancellationToken>((evt, _) => events.Add(evt))
            .Returns(Task.CompletedTask);

        var trigger1 = new Mock<INotificationTrigger<TestNotification>>();
        trigger1.Setup(t => t.MapEvent(It.IsAny<TestNotification>()))
            .Returns([
                new TriggerEvent { TriggerAlias = "trigger1", InitiatorType = "system" },
            ]);

        var trigger2 = new Mock<INotificationTrigger<TestNotification>>();
        trigger2.Setup(t => t.MapEvent(It.IsAny<TestNotification>()))
            .Returns([
                new TriggerEvent { TriggerAlias = "trigger2a", InitiatorType = "system" },
                new TriggerEvent { TriggerAlias = "trigger2b", InitiatorType = "system" },
            ]);

        var handler = new TriggerNotificationHandler<TestNotification>(
            [trigger1.Object, trigger2.Object],
            dispatcher.Object,
            CreateAcceptAllRegistry().Object,
            CreateEmptyOriginAccessor(),
            CreateRunningRuntimeState().Object,
            Mock.Of<ILogger<TriggerNotificationHandler<TestNotification>>>());

        await handler.HandleAsync(new TestNotification(), CancellationToken.None);

        events.Count.ShouldBe(3);
        events[0].TriggerAlias.ShouldBe("trigger1");
        events[1].TriggerAlias.ShouldBe("trigger2a");
        events[2].TriggerAlias.ShouldBe("trigger2b");
    }

    [Fact]
    public async Task HandleAsync_NoTriggers_DoesNothing()
    {
        var dispatcher = new Mock<ITriggerDispatcher>();
        var handler = new TriggerNotificationHandler<TestNotification>(
            [],
            dispatcher.Object,
            CreateAcceptAllRegistry().Object,
            CreateEmptyOriginAccessor(),
            CreateRunningRuntimeState().Object,
            Mock.Of<ILogger<TriggerNotificationHandler<TestNotification>>>());

        await handler.HandleAsync(new TestNotification(), CancellationToken.None);

        dispatcher.Verify(d => d.DispatchAsync(It.IsAny<TriggerEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_NotRunLevel_SkipsDispatch()
    {
        var dispatcher = new Mock<ITriggerDispatcher>();
        var runtimeState = new Mock<IRuntimeState>();
        runtimeState.Setup(r => r.Level).Returns(RuntimeLevel.Install);

        var trigger = new Mock<INotificationTrigger<TestNotification>>();
        trigger.Setup(t => t.MapEvent(It.IsAny<TestNotification>()))
            .Returns([new TriggerEvent { TriggerAlias = "test", InitiatorType = "system" }]);

        var handler = new TriggerNotificationHandler<TestNotification>(
            [trigger.Object],
            dispatcher.Object,
            CreateAcceptAllRegistry().Object,
            CreateEmptyOriginAccessor(),
            runtimeState.Object,
            Mock.Of<ILogger<TriggerNotificationHandler<TestNotification>>>());

        await handler.HandleAsync(new TestNotification(), CancellationToken.None);

        dispatcher.Verify(d => d.DispatchAsync(It.IsAny<TriggerEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_NoSubscribers_SkipsTrigger()
    {
        // Trigger implements IStepType so the registry can be consulted.
        var trigger = new Mock<INotificationTriggerWithMetadata>();
        trigger.As<IStepType>().SetupGet(t => t.Alias).Returns("test.alias");

        var registry = new Mock<ITriggerSubscriptionRegistry>();
        registry.Setup(r => r.HasSubscribersAsync("test.alias", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var dispatcher = new Mock<ITriggerDispatcher>();

        var handler = new TriggerNotificationHandler<TestNotification>(
            [trigger.Object],
            dispatcher.Object,
            registry.Object,
            CreateEmptyOriginAccessor(),
            CreateRunningRuntimeState().Object,
            Mock.Of<ILogger<TriggerNotificationHandler<TestNotification>>>());

        await handler.HandleAsync(new TestNotification(), CancellationToken.None);

        // MapEvent should not even be invoked when the registry says there are no subscribers
        // — that's the whole point of the optimisation.
        trigger.Verify(t => t.MapEvent(It.IsAny<TestNotification>()), Times.Never);
        dispatcher.Verify(d => d.DispatchAsync(It.IsAny<TriggerEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_HasSubscribers_DispatchesNormally()
    {
        var trigger = new Mock<INotificationTriggerWithMetadata>();
        trigger.As<IStepType>().SetupGet(t => t.Alias).Returns("test.alias");
        trigger.Setup(t => t.MapEvent(It.IsAny<TestNotification>()))
            .Returns([new TriggerEvent { TriggerAlias = "test.alias", InitiatorType = "system" }]);

        var registry = new Mock<ITriggerSubscriptionRegistry>();
        registry.Setup(r => r.HasSubscribersAsync("test.alias", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var dispatcher = new Mock<ITriggerDispatcher>();

        var handler = new TriggerNotificationHandler<TestNotification>(
            [trigger.Object],
            dispatcher.Object,
            registry.Object,
            CreateEmptyOriginAccessor(),
            CreateRunningRuntimeState().Object,
            Mock.Of<ILogger<TriggerNotificationHandler<TestNotification>>>());

        await handler.HandleAsync(new TestNotification(), CancellationToken.None);

        dispatcher.Verify(d => d.DispatchAsync(It.IsAny<TriggerEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    public class TestNotification : INotification;

    /// <summary>
    /// Composite interface used by the mocking framework to spin up a trigger that implements
    /// both <see cref="INotificationTrigger{T}"/> (the dispatch contract) and <see cref="IStepType"/>
    /// (where the alias lives) — same shape as real triggers extending <c>TriggerBase</c>.
    /// </summary>
    public interface INotificationTriggerWithMetadata : INotificationTrigger<TestNotification>, IStepType;
}
