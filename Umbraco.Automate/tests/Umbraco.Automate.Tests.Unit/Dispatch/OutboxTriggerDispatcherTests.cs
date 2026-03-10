using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Dispatch;
using Umbraco.Automate.Core.Messaging;
using Umbraco.Automate.Core.Triggers;

namespace Umbraco.Automate.Tests.Unit.Dispatch;

public class OutboxTriggerDispatcherTests
{
    private readonly Mock<IOutbox> _outbox = new();
    private readonly OutboxTriggerDispatcher _sut;

    public OutboxTriggerDispatcherTests()
    {
        _sut = new OutboxTriggerDispatcher(
            _outbox.Object,
            Mock.Of<ILogger<OutboxTriggerDispatcher>>());
    }

    [Fact]
    public async Task DispatchAsync_PublishesToCorrectTopicWithCorrectMessageFields()
    {
        object? captured = null;
        _outbox
            .Setup(p => p.PublishAsync(
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, object, CancellationToken>(
                (_, msg, _) => captured = msg)
            .Returns(Task.CompletedTask);

        var triggerEvent = new TriggerEvent
        {
            TriggerAlias = "contentPublished",
            InitiatorType = "system",
        };

        await _sut.DispatchAsync(triggerEvent, CancellationToken.None);

        _outbox.Verify(
            p => p.PublishAsync(
                OutboxTriggerDispatcher.TopicName,
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        captured.ShouldNotBeNull();
        var msg = captured.ShouldBeOfType<TriggerEventMessage>();
        msg.TriggerAlias.ShouldBe("contentPublished");
        msg.InitiatorType.ShouldBe("system");
    }

    [Fact]
    public async Task DispatchAsync_TypedTriggerEvent_IncludesSerializedOutputAndTypeName()
    {
        object? captured = null;
        _outbox
            .Setup(p => p.PublishAsync(
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, object, CancellationToken>(
                (_, msg, _) => captured = msg)
            .Returns(Task.CompletedTask);

        var triggerEvent = new TriggerEvent<TestOutput>
        {
            TriggerAlias = "contentPublished",
            InitiatorType = "system",
            Output = new TestOutput { Name = "Hello" },
        };

        await _sut.DispatchAsync(triggerEvent, CancellationToken.None);

        captured.ShouldNotBeNull();
        var msg = captured.ShouldBeOfType<TriggerEventMessage>();
        msg.OutputData.ShouldNotBeNullOrEmpty();
        msg.OutputData.ShouldContain("Hello");
        msg.OutputTypeName.ShouldNotBeNullOrEmpty();
        msg.OutputTypeName.ShouldContain(nameof(TestOutput));
    }

    [Fact]
    public async Task DispatchAsync_PlainTriggerEvent_LeavesOutputDataAndTypeNameNull()
    {
        object? captured = null;
        _outbox
            .Setup(p => p.PublishAsync(
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, object, CancellationToken>(
                (_, msg, _) => captured = msg)
            .Returns(Task.CompletedTask);

        var triggerEvent = new TriggerEvent
        {
            TriggerAlias = "manual",
            InitiatorType = "user",
        };

        await _sut.DispatchAsync(triggerEvent, CancellationToken.None);

        captured.ShouldNotBeNull();
        var msg = captured.ShouldBeOfType<TriggerEventMessage>();
        msg.OutputData.ShouldBeNull();
        msg.OutputTypeName.ShouldBeNull();
    }

    [Fact]
    public async Task DispatchAsync_InitiatorId_IsPassedThrough()
    {
        object? captured = null;
        _outbox
            .Setup(p => p.PublishAsync(
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, object, CancellationToken>(
                (_, msg, _) => captured = msg)
            .Returns(Task.CompletedTask);

        var triggerEvent = new TriggerEvent
        {
            TriggerAlias = "contentPublished",
            InitiatorType = "user",
            InitiatorId = "user-42",
        };

        await _sut.DispatchAsync(triggerEvent, CancellationToken.None);

        captured.ShouldNotBeNull();
        var msg = captured.ShouldBeOfType<TriggerEventMessage>();
        msg.InitiatorId.ShouldBe("user-42");
    }

    private class TestOutput
    {
        public string Name { get; set; } = string.Empty;
    }
}
