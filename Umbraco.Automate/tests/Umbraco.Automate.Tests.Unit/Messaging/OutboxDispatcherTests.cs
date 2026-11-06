using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Diagnostics;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Messaging;
using Umbraco.Cms.Core.Sync;

namespace Umbraco.Automate.Tests.Unit.Messaging;

public class OutboxDispatcherTests
{
    private readonly Mock<IOutboxStore> _store = new();
    private readonly AutomateReadinessSignal _readinessSignal = new();

    public OutboxDispatcherTests()
    {
        _readinessSignal.Signal();
        _store
            .Setup(s => s.GetStatsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OutboxStats { Pending = 1 });
        _store
            .Setup(s => s.ClaimNextAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OutboxMessage?)null);
    }

    [Fact]
    public async Task ExecuteAsync_LeavesMessagesPending_WhenEngineStartFailed()
    {
        // Claiming a trigger message would start a run nothing on this node can execute.
        // Unclaimed, it stays Pending and dispatches after a restart that recovers.
        _readinessSignal.SignalEngineStartFailed();
        var eligibility = new ExecutionNodeEligibility(
            Mock.Of<IServerRoleAccessor>(a => a.CurrentServerRole == ServerRole.Single),
            Options.Create(new ExecutionOptions { Mode = ExecutionMode.Distributed }),
            _readinessSignal);
        var handler = new EligibilityGatedHandler(eligibility);
        using var dispatcher = CreateDispatcher(handler, eligibility);

        await dispatcher.StartAsync(CancellationToken.None);
        await handler.PolledSeveralTimes.WaitAsync(TimeSpan.FromSeconds(5));
        await dispatcher.StopAsync(CancellationToken.None);

        _store.Verify(
            s => s.ClaimNextAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private OutboxDispatcher CreateDispatcher(IMessageHandler handler, IExecutionNodeEligibility eligibility)
    {
        var meterFactory = new Mock<IMeterFactory>();
        meterFactory
            .Setup(f => f.Create(It.IsAny<MeterOptions>()))
            .Returns((MeterOptions opts) => new Meter(opts.Name));

        return new OutboxDispatcher(
            _store.Object,
            [handler],
            _readinessSignal,
            Options.Create(new OutboxOptions { PollInterval = TimeSpan.FromMilliseconds(10) }),
            new AutomateMetrics(meterFactory.Object),
            eligibility,
            NullLogger<OutboxDispatcher>.Instance);
    }

    /// <summary>
    /// Stands in for the trigger, workflow queue and event queue handlers, which all gate
    /// <see cref="IMessageHandler.CanProcessNow"/> on <see cref="IExecutionNodeEligibility"/>.
    /// </summary>
    private sealed class EligibilityGatedHandler : IMessageHandler
    {
        private readonly IExecutionNodeEligibility _eligibility;
        private readonly TaskCompletionSource _polledSeveralTimes = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _polls;

        public EligibilityGatedHandler(IExecutionNodeEligibility eligibility) => _eligibility = eligibility;

        public Task PolledSeveralTimes => _polledSeveralTimes.Task;

        public string Topic => "test";

        public bool CanProcessNow()
        {
            if (Interlocked.Increment(ref _polls) >= 3)
            {
                _polledSeveralTimes.TrySetResult();
            }

            return _eligibility.CanExecuteWorkflows();
        }

        public Task HandleAsync(string body, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
