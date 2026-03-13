using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Dispatch;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.Scheduling;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Runtime;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Sync;

namespace Umbraco.Automate.Tests.Unit.Triggers;

public class ScheduledTriggerBackgroundJobTests
{
    private const string ScheduledTriggerAlias = "umbracoAutomate.scheduled";

    private readonly Mock<IAutomationService> _automationService = new();
    private readonly Mock<ITrigger> _mockTrigger = new();
    private readonly Mock<IScheduledTriggerStateStore> _stateStore = new();
    private readonly Mock<ITriggerDispatcher> _dispatcher = new();
    private readonly Mock<IRuntimeState> _runtimeState = new();
    private readonly Mock<IServerRoleAccessor> _serverRoleAccessor = new();
    private readonly Mock<IMainDom> _mainDom = new();
    private readonly ScheduledTriggerBackgroundJob _job;

    public ScheduledTriggerBackgroundJobTests()
    {
        _mockTrigger.Setup(t => t.Alias).Returns(ScheduledTriggerAlias);

        var triggerCollection = new TriggerCollection(() => [_mockTrigger.Object]);

        var services = new ServiceCollection();
        services.AddSingleton(_automationService.Object);
        services.AddSingleton(triggerCollection);
        services.AddSingleton(_stateStore.Object);
        services.AddSingleton(_dispatcher.Object);
        var sp = services.BuildServiceProvider();

        var options = Mock.Of<IOptionsMonitor<ScheduledTriggerOptions>>(
            o => o.CurrentValue == new ScheduledTriggerOptions());

        _runtimeState.Setup(r => r.Level).Returns(RuntimeLevel.Run);
        _serverRoleAccessor.Setup(r => r.CurrentServerRole).Returns(ServerRole.Single);
        _mainDom.Setup(m => m.IsMainDom).Returns(true);

        _job = new ScheduledTriggerBackgroundJob(
            sp,
            options,
            _runtimeState.Object,
            _serverRoleAccessor.Object,
            _mainDom.Object,
            Mock.Of<ILogger<ScheduledTriggerBackgroundJob>>());
    }

    [Fact]
    public async Task PerformExecuteAsync_RuntimeNotRun_Skips()
    {
        _runtimeState.Setup(r => r.Level).Returns(RuntimeLevel.Boot);

        await _job.PerformExecuteAsync(null);

        _automationService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PerformExecuteAsync_SubscriberRole_Skips()
    {
        _serverRoleAccessor.Setup(r => r.CurrentServerRole).Returns(ServerRole.Subscriber);

        await _job.PerformExecuteAsync(null);

        _automationService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PerformExecuteAsync_NotMainDom_Skips()
    {
        _mainDom.Setup(m => m.IsMainDom).Returns(false);

        await _job.PerformExecuteAsync(null);

        _automationService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PerformExecuteAsync_ScheduledTriggerDue_Dispatches()
    {
        var automationId = Guid.NewGuid();

        _automationService.Setup(s => s.GetPublishedVersionReferencesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(Guid Id, int PublishedVersion)> { (automationId, 1) });

        var automation = new Automation
        {
            Alias = "test",
            Name = "Test",
            IsEnabled = true,
            Status = AutomationStatus.Published,
            Trigger = new TriggerConfiguration
            {
                TriggerAlias = ScheduledTriggerAlias,
                Settings = [],
            },
        };
        _automationService.Setup(s => s.GetAutomationAsync(automationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        _mockTrigger.Setup(t => t.SettingsType).Returns((Type?)null);
        _mockTrigger.As<IScheduledTrigger>()
            .Setup(t => t.GetCronExpression(It.IsAny<object?>()))
            .Returns("* * * * *"); // Every minute

        // Last fired 2 minutes ago — next occurrence should be in the past.
        _stateStore.Setup(s => s.GetLastFiredAsync(automationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DateTime.UtcNow.AddMinutes(-2));

        await _job.PerformExecuteAsync(null);

        _dispatcher.Verify(
            d => d.DispatchAsync(
                It.Is<TriggerEvent>(e => e.TriggerAlias == ScheduledTriggerAlias && e.InitiatorType == "scheduled"),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _stateStore.Verify(
            s => s.SetLastFiredAsync(automationId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PerformExecuteAsync_TriggerNotDueYet_DoesNotDispatch()
    {
        var automationId = Guid.NewGuid();

        _automationService.Setup(s => s.GetPublishedVersionReferencesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(Guid Id, int PublishedVersion)> { (automationId, 1) });

        var automation = new Automation
        {
            Alias = "test",
            Name = "Test",
            IsEnabled = true,
            Status = AutomationStatus.Published,
            Trigger = new TriggerConfiguration
            {
                TriggerAlias = ScheduledTriggerAlias,
                Settings = [],
            },
        };
        _automationService.Setup(s => s.GetAutomationAsync(automationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        _mockTrigger.Setup(t => t.SettingsType).Returns((Type?)null);
        _mockTrigger.As<IScheduledTrigger>()
            .Setup(t => t.GetCronExpression(It.IsAny<object?>()))
            .Returns("0 0 1 1 *"); // Once a year — Jan 1 at midnight

        // Last fired recently.
        _stateStore.Setup(s => s.GetLastFiredAsync(automationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DateTime.UtcNow.AddSeconds(-10));

        await _job.PerformExecuteAsync(null);

        _dispatcher.Verify(
            d => d.DispatchAsync(It.IsAny<TriggerEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PerformExecuteAsync_DisabledAutomation_DoesNotDispatch()
    {
        var automationId = Guid.NewGuid();

        _automationService.Setup(s => s.GetPublishedVersionReferencesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(Guid Id, int PublishedVersion)> { (automationId, 1) });

        var automation = new Automation
        {
            Alias = "test",
            Name = "Test",
            IsEnabled = false,
            Trigger = new TriggerConfiguration { TriggerAlias = ScheduledTriggerAlias, Settings = [] },
        };
        _automationService.Setup(s => s.GetAutomationAsync(automationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        await _job.PerformExecuteAsync(null);

        _dispatcher.Verify(
            d => d.DispatchAsync(It.IsAny<TriggerEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
