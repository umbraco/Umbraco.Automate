using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Diagnostics;
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
        var meterFactory = CreateMeterFactory();
        _sut = new OutboxTriggerDispatcher(
            _outbox.Object,
            new AutomateMetrics(meterFactory),
            Mock.Of<ILogger<OutboxTriggerDispatcher>>());
    }

    private static IMeterFactory CreateMeterFactory()
    {
        var mock = new Mock<IMeterFactory>();
        mock.Setup(f => f.Create(It.IsAny<MeterOptions>()))
            .Returns((MeterOptions opts) => new Meter(opts.Name));
        return mock.Object;
    }

    [Fact]
    public async Task DispatchAsync_PublishesToCorrectTopicWithCorrectMessageFields()
    {
        object? captured = null;
        _outbox
            .Setup(p => p.PublishAsync(
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<string?>()))
            .Callback<string, object, CancellationToken, string?>(
                (_, msg, _, _) => captured = msg)
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
                It.IsAny<CancellationToken>(),
                It.IsAny<string?>()),
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
                It.IsAny<CancellationToken>(),
                It.IsAny<string?>()))
            .Callback<string, object, CancellationToken, string?>(
                (_, msg, _, _) => captured = msg)
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
                It.IsAny<CancellationToken>(),
                It.IsAny<string?>()))
            .Callback<string, object, CancellationToken, string?>(
                (_, msg, _, _) => captured = msg)
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
                It.IsAny<CancellationToken>(),
                It.IsAny<string?>()))
            .Callback<string, object, CancellationToken, string?>(
                (_, msg, _, _) => captured = msg)
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

    [Fact]
    public async Task DispatchAsync_IdempotencyKey_IsPassedToOutbox()
    {
        string? capturedDedup = null;
        _outbox
            .Setup(p => p.PublishAsync(
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<string?>()))
            .Callback<string, object, CancellationToken, string?>(
                (_, _, _, dedup) => capturedDedup = dedup)
            .Returns(Task.CompletedTask);

        var triggerEvent = new TriggerEvent
        {
            TriggerAlias = "contentPublished",
            InitiatorType = "system",
            IdempotencyKey = "contentPublished:abc-123:2026-03-11T10:00:00Z",
        };

        await _sut.DispatchAsync(triggerEvent, CancellationToken.None);

        capturedDedup.ShouldBe("contentPublished:abc-123:2026-03-11T10:00:00Z");
    }

    [Fact]
    public async Task DispatchAsync_NoIdempotencyKey_PassesNullToOutbox()
    {
        string? capturedDedup = "not-null";
        _outbox
            .Setup(p => p.PublishAsync(
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<string?>()))
            .Callback<string, object, CancellationToken, string?>(
                (_, _, _, dedup) => capturedDedup = dedup)
            .Returns(Task.CompletedTask);

        var triggerEvent = new TriggerEvent
        {
            TriggerAlias = "manual",
            InitiatorType = "user",
        };

        await _sut.DispatchAsync(triggerEvent, CancellationToken.None);

        capturedDedup.ShouldBeNull();
    }

    private class TestOutput
    {
        public string Name { get; set; } = string.Empty;
    }
}
