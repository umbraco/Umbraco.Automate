using DotNetCore.CAP;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Dispatch;
using Umbraco.Automate.Core.Triggers;

namespace Umbraco.Automate.Tests.Unit.Dispatch;

public class CapTriggerDispatcherTests
{
    private readonly Mock<ICapPublisher> _capPublisher = new();
    private readonly CapTriggerDispatcher _sut;

    public CapTriggerDispatcherTests()
    {
        _sut = new CapTriggerDispatcher(
            _capPublisher.Object,
            Mock.Of<ILogger<CapTriggerDispatcher>>());
    }

    [Fact]
    public async Task DispatchAsync_PublishesToCorrectTopicWithCorrectMessageFields()
    {
        TriggerEventMessage? captured = null;
        _capPublisher
            .Setup(p => p.PublishAsync(
                It.IsAny<string>(),
                It.IsAny<TriggerEventMessage>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, TriggerEventMessage?, string?, CancellationToken>(
                (_, msg, _, _) => captured = msg)
            .Returns(Task.CompletedTask);

        var triggerEvent = new TriggerEvent
        {
            TriggerAlias = "contentPublished",
            InitiatorType = "system",
        };

        await _sut.DispatchAsync(triggerEvent, CancellationToken.None);

        _capPublisher.Verify(
            p => p.PublishAsync(
                CapTriggerDispatcher.TopicName,
                It.IsAny<TriggerEventMessage>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        captured.ShouldNotBeNull();
        captured.TriggerAlias.ShouldBe("contentPublished");
        captured.InitiatorType.ShouldBe("system");
    }

    [Fact]
    public async Task DispatchAsync_TypedTriggerEvent_IncludesSerializedOutputAndTypeName()
    {
        TriggerEventMessage? captured = null;
        _capPublisher
            .Setup(p => p.PublishAsync(
                It.IsAny<string>(),
                It.IsAny<TriggerEventMessage>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, TriggerEventMessage?, string?, CancellationToken>(
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
        captured.OutputData.ShouldNotBeNullOrEmpty();
        captured.OutputData.ShouldContain("Hello");
        captured.OutputTypeName.ShouldNotBeNullOrEmpty();
        captured.OutputTypeName.ShouldContain(nameof(TestOutput));
    }

    [Fact]
    public async Task DispatchAsync_PlainTriggerEvent_LeavesOutputDataAndTypeNameNull()
    {
        TriggerEventMessage? captured = null;
        _capPublisher
            .Setup(p => p.PublishAsync(
                It.IsAny<string>(),
                It.IsAny<TriggerEventMessage>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, TriggerEventMessage?, string?, CancellationToken>(
                (_, msg, _, _) => captured = msg)
            .Returns(Task.CompletedTask);

        var triggerEvent = new TriggerEvent
        {
            TriggerAlias = "manual",
            InitiatorType = "user",
        };

        await _sut.DispatchAsync(triggerEvent, CancellationToken.None);

        captured.ShouldNotBeNull();
        captured.OutputData.ShouldBeNull();
        captured.OutputTypeName.ShouldBeNull();
    }

    [Fact]
    public async Task DispatchAsync_InitiatorId_IsPassedThrough()
    {
        TriggerEventMessage? captured = null;
        _capPublisher
            .Setup(p => p.PublishAsync(
                It.IsAny<string>(),
                It.IsAny<TriggerEventMessage>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, TriggerEventMessage?, string?, CancellationToken>(
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
        captured.InitiatorId.ShouldBe("user-42");
    }

    private class TestOutput
    {
        public string Name { get; set; } = string.Empty;
    }
}
