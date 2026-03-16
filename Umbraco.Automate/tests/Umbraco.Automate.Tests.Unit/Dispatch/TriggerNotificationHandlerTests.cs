using Moq;
using Shouldly;
using Umbraco.Automate.Core.Dispatch;
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
            CreateRunningRuntimeState().Object);

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
            CreateRunningRuntimeState().Object);

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
            runtimeState.Object);

        await handler.HandleAsync(new TestNotification(), CancellationToken.None);

        dispatcher.Verify(d => d.DispatchAsync(It.IsAny<TriggerEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    public class TestNotification : INotification;
}
