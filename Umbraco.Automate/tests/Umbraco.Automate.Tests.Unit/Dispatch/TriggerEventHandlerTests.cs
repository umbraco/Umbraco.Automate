using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Dispatch;
using Umbraco.Automate.Core.Execution;
using Umbraco.Cms.Core.Sync;

namespace Umbraco.Automate.Tests.Unit.Dispatch;

public class TriggerEventHandlerTests
{
    private readonly Mock<IAutomationService> _automationService = new();
    private readonly Mock<IAutomationExecutor> _executor = new();
    private readonly Mock<IServerRoleAccessor> _serverRoleAccessor = new();
    private readonly TriggerEventHandler _handler;

    public TriggerEventHandlerTests()
    {
        _serverRoleAccessor.Setup(s => s.CurrentServerRole).Returns(ServerRole.Single);

        _handler = new TriggerEventHandler(
            _automationService.Object,
            _executor.Object,
            _serverRoleAccessor.Object,
            Options.Create(new ExecutionOptions()),
            Mock.Of<ILogger<TriggerEventHandler>>());
    }

    [Fact]
    public async Task HandleAsync_MatchingAutomation_ExecutesRun()
    {
        var automation = CreatePublishedAutomation("myTrigger");
        _automationService.Setup(s => s.GetAllAutomationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { automation });

        var body = SerializeMessage(new TriggerEventMessage
        {
            TriggerAlias = "myTrigger",
            InitiatorType = "system",
        });

        await _handler.HandleAsync(body, CancellationToken.None);

        _executor.Verify(e => e.ExecuteAsync(
            automation,
            "system",
            null,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_NoMatchingAutomations_DoesNotExecute()
    {
        var automation = CreatePublishedAutomation("otherTrigger");
        _automationService.Setup(s => s.GetAllAutomationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { automation });

        var body = SerializeMessage(new TriggerEventMessage
        {
            TriggerAlias = "myTrigger",
            InitiatorType = "system",
        });

        await _handler.HandleAsync(body, CancellationToken.None);

        _executor.Verify(e => e.ExecuteAsync(
            It.IsAny<Automation>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<Dictionary<string, object?>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_DisabledAutomation_DoesNotExecute()
    {
        var automation = CreatePublishedAutomation("myTrigger");
        automation.IsEnabled = false;

        _automationService.Setup(s => s.GetAllAutomationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { automation });

        var body = SerializeMessage(new TriggerEventMessage
        {
            TriggerAlias = "myTrigger",
            InitiatorType = "system",
        });

        await _handler.HandleAsync(body, CancellationToken.None);

        _executor.Verify(e => e.ExecuteAsync(
            It.IsAny<Automation>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<Dictionary<string, object?>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_DraftAutomation_DoesNotExecute()
    {
        var automation = CreatePublishedAutomation("myTrigger");
        automation.Status = AutomationStatus.Draft;

        _automationService.Setup(s => s.GetAllAutomationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { automation });

        var body = SerializeMessage(new TriggerEventMessage
        {
            TriggerAlias = "myTrigger",
            InitiatorType = "system",
        });

        await _handler.HandleAsync(body, CancellationToken.None);

        _executor.Verify(e => e.ExecuteAsync(
            It.IsAny<Automation>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<Dictionary<string, object?>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_MultipleMatchingAutomations_ExecutesAll()
    {
        var a1 = CreatePublishedAutomation("myTrigger");
        var a2 = CreatePublishedAutomation("myTrigger");

        _automationService.Setup(s => s.GetAllAutomationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { a1, a2 });

        var body = SerializeMessage(new TriggerEventMessage
        {
            TriggerAlias = "myTrigger",
            InitiatorType = "system",
        });

        await _handler.HandleAsync(body, CancellationToken.None);

        _executor.Verify(e => e.ExecuteAsync(
            It.IsAny<Automation>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<Dictionary<string, object?>?>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task HandleAsync_WithOutputData_DeserializesAndPassesToExecutor()
    {
        var automation = CreatePublishedAutomation("myTrigger");
        _automationService.Setup(s => s.GetAllAutomationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { automation });

        Dictionary<string, object?>? capturedData = null;
        _executor.Setup(e => e.ExecuteAsync(
                It.IsAny<Automation>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Dictionary<string, object?>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<Automation, string, string?, Dictionary<string, object?>?, CancellationToken>(
                (_, _, _, data, _) => capturedData = data)
            .ReturnsAsync(Guid.NewGuid());

        var body = SerializeMessage(new TriggerEventMessage
        {
            TriggerAlias = "myTrigger",
            InitiatorType = "system",
            OutputData = "{\"contentName\":\"Hello\"}",
        });

        await _handler.HandleAsync(body, CancellationToken.None);

        capturedData.ShouldNotBeNull();
        capturedData.ShouldContainKey("contentName");
    }

    [Theory]
    [InlineData(ServerRole.Single, ExecutionMode.SchedulerOnly, true)]
    [InlineData(ServerRole.SchedulingPublisher, ExecutionMode.SchedulerOnly, true)]
    [InlineData(ServerRole.Subscriber, ExecutionMode.SchedulerOnly, false)]
    [InlineData(ServerRole.Unknown, ExecutionMode.SchedulerOnly, false)]
    [InlineData(ServerRole.Subscriber, ExecutionMode.Distributed, true)]
    [InlineData(ServerRole.Unknown, ExecutionMode.Distributed, true)]
    public async Task HandleAsync_RespectsExecutionModeAndServerRole(
        ServerRole role, ExecutionMode mode, bool shouldExecute)
    {
        var serverRole = new Mock<IServerRoleAccessor>();
        serverRole.Setup(s => s.CurrentServerRole).Returns(role);

        var automationService = new Mock<IAutomationService>();
        var executor = new Mock<IAutomationExecutor>();

        var automation = CreatePublishedAutomation("myTrigger");
        automationService.Setup(s => s.GetAllAutomationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { automation });

        var handler = new TriggerEventHandler(
            automationService.Object,
            executor.Object,
            serverRole.Object,
            Options.Create(new ExecutionOptions { Mode = mode }),
            Mock.Of<ILogger<TriggerEventHandler>>());

        var body = SerializeMessage(new TriggerEventMessage
        {
            TriggerAlias = "myTrigger",
            InitiatorType = "system",
        });

        await handler.HandleAsync(body, CancellationToken.None);

        executor.Verify(
            e => e.ExecuteAsync(
                It.IsAny<Automation>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Dictionary<string, object?>?>(),
                It.IsAny<CancellationToken>()),
            shouldExecute ? Times.Once : Times.Never);
    }

    private static string SerializeMessage(TriggerEventMessage message)
        => JsonSerializer.Serialize(message, JsonOptions.Default);

    private static Automation CreatePublishedAutomation(string triggerAlias) => new()
    {
        Id = Guid.NewGuid(),
        Alias = "test-automation",
        Name = "Test Automation",
        Status = AutomationStatus.Published,
        IsEnabled = true,
        Trigger = new TriggerConfiguration { TriggerAlias = triggerAlias },
    };
}
