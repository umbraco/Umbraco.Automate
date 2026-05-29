using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Scoping;

namespace Umbraco.Automate.Tests.Unit.Automations;

public class CircuitBreakerServiceTests
{
    private readonly Mock<IAutomationHealthRepository> _healthRepo = new();
    private readonly Mock<IAutomationRunService> _runService = new();
    private CircuitBreakerOptions _options = new();
    private AutomationHealthState? _saved;

    private CircuitBreakerService CreateService()
    {
        var optionsMonitor = new Mock<IOptionsMonitor<CircuitBreakerOptions>>();
        optionsMonitor.Setup(o => o.CurrentValue).Returns(() => _options);

        _healthRepo
            .Setup(r => r.SaveAsync(It.IsAny<AutomationHealthState>(), It.IsAny<CancellationToken>()))
            .Callback<AutomationHealthState, CancellationToken>((s, _) => _saved = s)
            .Returns(Task.CompletedTask);

        var scope = new Mock<ICoreScope>();
        scope.Setup(s => s.Notifications).Returns(Mock.Of<IScopedNotificationPublisher>());

        var scopeProvider = new Mock<ICoreScopeProvider>();
        scopeProvider
            .Setup(s => s.CreateCoreScope(
                It.IsAny<System.Data.IsolationLevel>(),
                It.IsAny<RepositoryCacheMode>(),
                It.IsAny<IEventDispatcher?>(),
                It.IsAny<IScopedNotificationPublisher?>(),
                It.IsAny<bool?>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .Returns(scope.Object);

        var eventMessagesFactory = new Mock<IEventMessagesFactory>();
        eventMessagesFactory.Setup(f => f.Get()).Returns(new EventMessages());

        return new CircuitBreakerService(
            optionsMonitor.Object,
            _healthRepo.Object,
            _runService.Object,
            scopeProvider.Object,
            eventMessagesFactory.Object,
            NullLogger<CircuitBreakerService>.Instance);
    }

    private void SetupHealth(Guid automationId, AutomationHealthState? state)
        => _healthRepo
            .Setup(r => r.GetAsync(automationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);

    private void SetupWindow(Guid automationId, params AutomationRunStatus[] newestFirst)
        => _runService
            .Setup(r => r.GetRecentTerminalStatusesAsync(
                automationId,
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(newestFirst);

    private static AutomationRun Run(AutomationRunStatus status, Guid automationId) => new()
    {
        AutomationId = automationId,
        AutomationVersion = 1,
        WorkspaceId = Guid.NewGuid(),
        ServiceAccountKey = Guid.NewGuid(),
        InitiatedBy = TriggerInitiatorType.System,
        Status = status,
    };

    [Fact]
    public async Task EvaluateAsync_SuccessfulRun_Degraded_ResetsToHealthy()
    {
        var id = Guid.NewGuid();
        SetupHealth(id, new AutomationHealthState { AutomationId = id, Health = AutomationHealth.Degraded, WarningIssuedUtc = DateTime.UtcNow });
        var service = CreateService();

        await service.EvaluateAsync(Run(AutomationRunStatus.Completed, id), CancellationToken.None);

        _saved.ShouldNotBeNull();
        _saved!.Health.ShouldBe(AutomationHealth.Healthy);
        _saved.WarningIssuedUtc.ShouldBeNull();
    }

    [Fact]
    public async Task EvaluateAsync_SuccessfulRun_Disabled_DoesNotReset()
    {
        var id = Guid.NewGuid();
        SetupHealth(id, new AutomationHealthState { AutomationId = id, Health = AutomationHealth.Disabled, DisabledUtc = DateTime.UtcNow });
        var service = CreateService();

        await service.EvaluateAsync(Run(AutomationRunStatus.Completed, id), CancellationToken.None);

        _saved.ShouldBeNull();
    }

    [Fact]
    public async Task EvaluateAsync_SuspendedRun_Ignored()
    {
        var id = Guid.NewGuid();
        var service = CreateService();

        await service.EvaluateAsync(Run(AutomationRunStatus.Suspended, id), CancellationToken.None);

        _saved.ShouldBeNull();
        _runService.Verify(
            r => r.GetRecentTerminalStatusesAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EvaluateAsync_ConsecutiveFailuresReachThreshold_Disables()
    {
        var id = Guid.NewGuid();
        _options = new CircuitBreakerOptions { ConsecutiveFailureThreshold = 3 };
        SetupHealth(id, null);
        SetupWindow(id, AutomationRunStatus.Failed, AutomationRunStatus.Failed, AutomationRunStatus.Failed);
        var service = CreateService();

        await service.EvaluateAsync(Run(AutomationRunStatus.Failed, id), CancellationToken.None);

        _saved.ShouldNotBeNull();
        _saved!.Health.ShouldBe(AutomationHealth.Disabled);
        _saved.DisabledUtc.ShouldNotBeNull();
    }

    [Fact]
    public async Task EvaluateAsync_ConsecutiveFailures_ReachWarningThreshold_IssuesWarning()
    {
        var id = Guid.NewGuid();
        _options = new CircuitBreakerOptions
        {
            ConsecutiveWarningThreshold = 5,
            ConsecutiveFailureThreshold = 10,
        };
        SetupHealth(id, null);
        // 5 consecutive Failed (window not full — rate path wouldn't fire), so consecutive-warning
        // is the one that should issue the warning before consecutive-disable trips at 10.
        SetupWindow(id,
            AutomationRunStatus.Failed,
            AutomationRunStatus.Failed,
            AutomationRunStatus.Failed,
            AutomationRunStatus.Failed,
            AutomationRunStatus.Failed);
        var service = CreateService();

        await service.EvaluateAsync(Run(AutomationRunStatus.Failed, id), CancellationToken.None);

        _saved.ShouldNotBeNull();
        _saved!.Health.ShouldBe(AutomationHealth.Degraded);
        _saved.WarningIssuedUtc.ShouldNotBeNull();
    }

    [Fact]
    public async Task EvaluateAsync_ErrorRateAboveWarning_IssuesWarning()
    {
        var id = Guid.NewGuid();
        _options = new CircuitBreakerOptions
        {
            ConsecutiveFailureThreshold = 100,
            EvaluationWindowSize = 4,
            WarningErrorRate = 0.5,
            DisableErrorRate = 0.9,
        };
        SetupHealth(id, null);
        // 2/4 failed, only the most recent is failed → consecutive 1, rate 0.5.
        SetupWindow(id, AutomationRunStatus.Failed, AutomationRunStatus.Completed, AutomationRunStatus.Failed, AutomationRunStatus.Completed);
        var service = CreateService();

        await service.EvaluateAsync(Run(AutomationRunStatus.Failed, id), CancellationToken.None);

        _saved.ShouldNotBeNull();
        _saved!.Health.ShouldBe(AutomationHealth.Degraded);
        _saved.WarningIssuedUtc.ShouldNotBeNull();
    }

    [Fact]
    public async Task EvaluateAsync_ErrorRateAboveDisable_AfterGrace_Disables()
    {
        var id = Guid.NewGuid();
        _options = new CircuitBreakerOptions
        {
            ConsecutiveFailureThreshold = 100,
            EvaluationWindowSize = 4,
            WarningErrorRate = 0.5,
            DisableErrorRate = 0.7,
            GracePeriodAfterWarning = TimeSpan.FromHours(1),
        };
        SetupHealth(id, new AutomationHealthState
        {
            AutomationId = id,
            Health = AutomationHealth.Degraded,
            WarningIssuedUtc = DateTime.UtcNow.AddHours(-2),
        });
        SetupWindow(id, AutomationRunStatus.Failed, AutomationRunStatus.Failed, AutomationRunStatus.Failed, AutomationRunStatus.Completed);
        var service = CreateService();

        await service.EvaluateAsync(Run(AutomationRunStatus.Failed, id), CancellationToken.None);

        _saved.ShouldNotBeNull();
        _saved!.Health.ShouldBe(AutomationHealth.Disabled);
    }

    [Fact]
    public async Task EvaluateAsync_ErrorRateAboveDisable_WithinGrace_DoesNotDisable()
    {
        var id = Guid.NewGuid();
        _options = new CircuitBreakerOptions
        {
            ConsecutiveFailureThreshold = 100,
            EvaluationWindowSize = 4,
            WarningErrorRate = 0.5,
            DisableErrorRate = 0.7,
            GracePeriodAfterWarning = TimeSpan.FromHours(1),
        };
        SetupHealth(id, new AutomationHealthState
        {
            AutomationId = id,
            Health = AutomationHealth.Degraded,
            WarningIssuedUtc = DateTime.UtcNow.AddMinutes(-10),
        });
        SetupWindow(id, AutomationRunStatus.Failed, AutomationRunStatus.Failed, AutomationRunStatus.Failed, AutomationRunStatus.Completed);
        var service = CreateService();

        await service.EvaluateAsync(Run(AutomationRunStatus.Failed, id), CancellationToken.None);

        _saved.ShouldBeNull();
    }

    [Fact]
    public async Task EvaluateAsync_FailedRun_AlreadyDisabled_IsIdempotentNoOp()
    {
        var id = Guid.NewGuid();
        SetupHealth(id, new AutomationHealthState { AutomationId = id, Health = AutomationHealth.Disabled, DisabledUtc = DateTime.UtcNow });
        var service = CreateService();

        await service.EvaluateAsync(Run(AutomationRunStatus.Failed, id), CancellationToken.None);

        _saved.ShouldBeNull();
        _runService.Verify(
            r => r.GetRecentTerminalStatusesAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EvaluateAsync_CircuitBreakerDisabledViaOptions_DoesNothing()
    {
        var id = Guid.NewGuid();
        _options = new CircuitBreakerOptions { Enabled = false };
        var service = CreateService();

        await service.EvaluateAsync(Run(AutomationRunStatus.Failed, id), CancellationToken.None);

        _saved.ShouldBeNull();
        _healthRepo.Verify(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(TriggerInitiatorType.System, false)]
    [InlineData(TriggerInitiatorType.Scheduled, false)]
    [InlineData(TriggerInitiatorType.Webhook, false)]
    [InlineData(TriggerInitiatorType.User, true)]
    [InlineData(TriggerInitiatorType.Replay, true)]
    public async Task IsRunAllowedAsync_Disabled_DependsOnInitiator(string initiatorType, bool expected)
    {
        var id = Guid.NewGuid();
        _options = new CircuitBreakerOptions { AllowManualRunWhileDisabled = true };
        SetupHealth(id, new AutomationHealthState { AutomationId = id, Health = AutomationHealth.Disabled });
        var service = CreateService();

        var allowed = await service.IsRunAllowedAsync(id, initiatorType, CancellationToken.None);

        allowed.ShouldBe(expected);
    }

    [Fact]
    public async Task IsRunAllowedAsync_Disabled_ManualNotAllowed_BlocksInteractive()
    {
        var id = Guid.NewGuid();
        _options = new CircuitBreakerOptions { AllowManualRunWhileDisabled = false };
        SetupHealth(id, new AutomationHealthState { AutomationId = id, Health = AutomationHealth.Disabled });
        var service = CreateService();

        (await service.IsRunAllowedAsync(id, TriggerInitiatorType.User, CancellationToken.None)).ShouldBeFalse();
    }

    [Fact]
    public async Task IsRunAllowedAsync_Healthy_AllowsAutomatic()
    {
        var id = Guid.NewGuid();
        SetupHealth(id, null);
        var service = CreateService();

        (await service.IsRunAllowedAsync(id, TriggerInitiatorType.System, CancellationToken.None)).ShouldBeTrue();
    }

    [Fact]
    public async Task ResetAsync_Disabled_ClearsToHealthy_AndAdvancesWindowFloor()
    {
        var id = Guid.NewGuid();
        var before = DateTime.UtcNow;
        SetupHealth(id, new AutomationHealthState
        {
            AutomationId = id,
            Health = AutomationHealth.Disabled,
            WarningIssuedUtc = DateTime.UtcNow.AddHours(-3),
            DisabledUtc = DateTime.UtcNow.AddHours(-1),
        });
        var service = CreateService();

        await service.ResetAsync(id, CancellationToken.None);

        _saved.ShouldNotBeNull();
        _saved!.Health.ShouldBe(AutomationHealth.Healthy);
        _saved.WarningIssuedUtc.ShouldBeNull();
        _saved.DisabledUtc.ShouldBeNull();
        _saved.WindowResetUtc.ShouldNotBeNull();
        _saved.WindowResetUtc!.Value.ShouldBeGreaterThanOrEqualTo(before);
    }

    [Fact]
    public async Task EvaluateAsync_FailedRun_QueriesWindowSinceLastResetFloor()
    {
        var id = Guid.NewGuid();
        var resetAt = DateTime.UtcNow.AddMinutes(-5);
        _options = new CircuitBreakerOptions { ConsecutiveFailureThreshold = 10 };
        SetupHealth(id, new AutomationHealthState
        {
            AutomationId = id,
            Health = AutomationHealth.Healthy,
            WindowResetUtc = resetAt,
        });
        // Only one Failed run since reset → consecutive 1 < threshold → no disable, even though
        // historic runs (pre-reset) had many failures.
        SetupWindow(id, AutomationRunStatus.Failed);
        var service = CreateService();

        await service.EvaluateAsync(Run(AutomationRunStatus.Failed, id), CancellationToken.None);

        _saved.ShouldBeNull();
        _runService.Verify(
            r => r.GetRecentTerminalStatusesAsync(id, It.IsAny<int>(), resetAt, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ResetAsync_NoState_NoOp()
    {
        var id = Guid.NewGuid();
        SetupHealth(id, null);
        var service = CreateService();

        await service.ResetAsync(id, CancellationToken.None);

        _saved.ShouldBeNull();
    }
}
