using Microsoft.Extensions.Options;
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

    private static ExecutionNodeEligibility Build(ServerRole role, ExecutionMode mode)
    {
        var serverRole = new Mock<IServerRoleAccessor>();
        serverRole.Setup(s => s.CurrentServerRole).Returns(role);
        return new ExecutionNodeEligibility(
            serverRole.Object,
            Options.Create(new ExecutionOptions { Mode = mode }));
    }
}
