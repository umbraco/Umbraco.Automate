using Microsoft.Extensions.Options;
using Umbraco.Automate.Core;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Execution;
using Umbraco.Cms.Core.Sync;

namespace Umbraco.Automate.Tests.Unit.Execution;

public class ExecutionNodeEligibilityTests
{
    [Theory]
    [InlineData(ServerRole.Single, true)]
    [InlineData(ServerRole.SchedulingPublisher, true)]
    [InlineData(ServerRole.Subscriber, false)]
    [InlineData(ServerRole.Unknown, false)]
    public void SchedulerOnly_AllowsOnlyPublisherOrSingle(ServerRole role, bool expected)
    {
        // Crucially, ServerRole.Unknown returns false — this is the role during startup
        // before Umbraco's elector designates the node, and the bug we're fixing was that
        // events were being silently dropped during this transient window.
        var sut = Build(role, ExecutionMode.SchedulerOnly);
        sut.CanExecuteWorkflows().ShouldBe(expected);
    }

    [Theory]
    [InlineData(ServerRole.Single)]
    [InlineData(ServerRole.SchedulingPublisher)]
    [InlineData(ServerRole.Subscriber)]
    [InlineData(ServerRole.Unknown)]
    public void Distributed_AllowsAnyRole(ServerRole role)
    {
        // In Distributed mode every node participates, so role doesn't gate execution.
        var sut = Build(role, ExecutionMode.Distributed);
        sut.CanExecuteWorkflows().ShouldBeTrue();
    }

    [Theory]
    [InlineData(ServerRole.Single, ExecutionMode.SchedulerOnly)]
    [InlineData(ServerRole.SchedulingPublisher, ExecutionMode.SchedulerOnly)]
    [InlineData(ServerRole.Unknown, ExecutionMode.Distributed)]
    public void WhenEligible_ReasonIsNull(ServerRole role, ExecutionMode mode)
    {
        var sut = Build(role, mode);
        sut.CanExecuteWorkflows(out var reason).ShouldBeTrue();
        reason.ShouldBeNull();
    }

    [Fact]
    public void WhenUnknown_ReasonExplainsElectionAndRemediation()
    {
        var sut = Build(ServerRole.Unknown, ExecutionMode.SchedulerOnly);

        sut.CanExecuteWorkflows(out var reason).ShouldBeFalse();
        reason.ShouldNotBeNull();
        reason.ShouldContain("Unknown");
        // Surfaces an actionable remediation rather than just stating the role.
        reason.ShouldContain("DisableElectionForSingleServer");
    }

    [Fact]
    public void WhenSubscriber_ReasonIsExpectedNotAlarming()
    {
        var sut = Build(ServerRole.Subscriber, ExecutionMode.SchedulerOnly);

        sut.CanExecuteWorkflows(out var reason).ShouldBeFalse();
        reason.ShouldNotBeNull();
        reason.ShouldContain("Subscriber");
        // Subscriber is a legitimate state in a multi-node setup, so the wording is reassuring.
        reason.ShouldContain("expected");
    }

    [Fact]
    public void WhenEngineStartFailed_ReturnsFalse()
    {
        // Even in Distributed mode, where every role is otherwise eligible: a node whose engine
        // never started must leave workflow work pending rather than claim it.
        var sut = Build(ServerRole.Single, ExecutionMode.Distributed, EngineStartFailedSignal());

        sut.CanExecuteWorkflows().ShouldBeFalse();
    }

    [Fact]
    public void WhenEngineStartFailed_ReasonExplainsRecoveryFailure()
    {
        var sut = Build(ServerRole.Single, ExecutionMode.Distributed, EngineStartFailedSignal());

        sut.CanExecuteWorkflows(out var reason);

        reason.ShouldNotBeNull().ShouldContain("startup recovery failed");
    }

    private static AutomateReadinessSignal EngineStartFailedSignal()
    {
        var signal = new AutomateReadinessSignal();
        signal.Signal();
        signal.SignalEngineStartFailed();
        return signal;
    }

    private static ExecutionNodeEligibility Build(
        ServerRole role,
        ExecutionMode mode,
        AutomateReadinessSignal? readinessSignal = null)
    {
        var serverRole = new Mock<IServerRoleAccessor>();
        serverRole.Setup(s => s.CurrentServerRole).Returns(role);
        return new ExecutionNodeEligibility(
            serverRole.Object,
            Options.Create(new ExecutionOptions { Mode = mode }),
            readinessSignal ?? new AutomateReadinessSignal());
    }
}
