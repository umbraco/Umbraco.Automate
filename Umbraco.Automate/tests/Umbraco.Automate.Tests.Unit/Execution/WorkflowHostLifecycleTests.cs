using Microsoft.Extensions.Logging.Abstractions;
using Umbraco.Automate.Core;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Execution;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace Umbraco.Automate.Tests.Unit.Execution;

public class WorkflowHostLifecycleTests
{
    private readonly Mock<IWorkflowHost> _workflowHost = new();
    private readonly Mock<IStuckRunRecovery> _stuckRunRecovery = new();
    private readonly Mock<IPersistenceProvider> _persistence = new();

    [Fact]
    public async Task ExecuteAsync_CompletesWithoutThrowing_WhenStuckRunRecoveryFails()
    {
        FailStuckRunRecovery();

        await Should.NotThrowAsync(RunLifecycleAsync);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotStartHost_WhenStuckRunRecoveryFails()
    {
        FailStuckRunRecovery();

        await RunLifecycleAsync();

        _workflowHost.Verify(h => h.Start(), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_CompletesWithoutThrowing_WhenDefinitionRecoveryFails()
    {
        FailDefinitionRecovery();

        await Should.NotThrowAsync(RunLifecycleAsync);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotStartHost_WhenDefinitionRecoveryFails()
    {
        FailDefinitionRecovery();

        await RunLifecycleAsync();

        _workflowHost.Verify(h => h.Start(), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_StartsHost_WhenRecoverySucceeds()
    {
        // Guards the fixture: without it, the DoesNotStartHost tests would also pass on an early return.
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _workflowHost.Setup(h => h.Start()).Callback(() => started.TrySetResult());
        using var lifecycle = CreateLifecycle();

        await lifecycle.StartAsync(CancellationToken.None);
        try
        {
            await Should.NotThrowAsync(() => started.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            await lifecycle.StopAsync(CancellationToken.None);
        }
    }

    private void FailStuckRunRecovery()
        => _stuckRunRecovery
            .Setup(r => r.RecoverStuckRunsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Simulated recovery failure"));

    private void FailDefinitionRecovery()
#pragma warning disable CS0612 // WorkflowDefinitionRecovery calls this obsolete overload, so the failure must come from it
        => _persistence
            .Setup(p => p.GetWorkflowInstances(
                It.IsAny<WorkflowStatus?>(),
                It.IsAny<string>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int>(),
                It.IsAny<int>()))
            .ThrowsAsync(new InvalidOperationException("Simulated recovery failure"));
#pragma warning restore CS0612

    private async Task RunLifecycleAsync()
    {
        using var lifecycle = CreateLifecycle();

        await lifecycle.StartAsync(CancellationToken.None);
        await lifecycle.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private WorkflowHostLifecycle CreateLifecycle()
    {
        var readinessSignal = new AutomateReadinessSignal();
        readinessSignal.Signal();

        var definitionRecovery = new WorkflowDefinitionRecovery(
            _persistence.Object,
            Mock.Of<IWorkflowRegistry>(),
            Mock.Of<IWorkflowCompiler>(),
            Mock.Of<IAutomationService>(),
            NullLogger<WorkflowDefinitionRecovery>.Instance);

        return new WorkflowHostLifecycle(
            _workflowHost.Object,
            _stuckRunRecovery.Object,
            definitionRecovery,
            readinessSignal,
            NullLogger<WorkflowHostLifecycle>.Instance);
    }
}
