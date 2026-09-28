using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.Webhooks;
using Umbraco.Automate.Persistence.Automations;
using Umbraco.Automate.Persistence.Notifications;
using Umbraco.Automate.Persistence.Runs;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Automate.Tests.Common.Fixtures;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Sync;

namespace Umbraco.Automate.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="StuckRunRecoveryNotificationHandler"/> against in-memory SQLite:
/// on <see cref="UmbracoApplicationStartedNotification"/>, and only once
/// <see cref="AutomateReadinessSignal"/> reports the schema is ready, runs the previous process
/// left in flight are marked failed. The failed-migration skip is covered by the unit tests.
/// </summary>
public class StuckRunRecoveryTests : IDisposable
{
    private const string InterruptedError = "Recovered after application restart — workflow was interrupted";

    private readonly EfCoreTestFixture _fixture;
    private readonly TestDbContextFactory _dbContextFactory;
    private readonly EFCoreAutomationRepository _automationRepository;
    private readonly EFCoreAutomationRunRepository _runRepository;
    private readonly AutomateReadinessSignal _readinessSignal = new();
    private readonly Mock<IServerRoleAccessor> _serverRoleAccessor = new();

    public StuckRunRecoveryTests()
    {
        _fixture = new EfCoreTestFixture();
        _dbContextFactory = new TestDbContextFactory(_fixture.CreateContext);
        _automationRepository = new EFCoreAutomationRepository(_dbContextFactory, CreateFactory());
        _runRepository = new EFCoreAutomationRunRepository(_dbContextFactory);
        _serverRoleAccessor.Setup(r => r.CurrentServerRole).Returns(ServerRole.Single);
    }

    [Fact]
    public async Task HandleAsync_AfterReadiness_MarksStuckRunAndItsStepsFailed()
    {
        var automation = await SaveAutomationAsync();
        var stuckRun = await SaveRunAsync(automation.Id, AutomationRunStatus.Running, StepRunStatus.Completed, StepRunStatus.Running);

        var handler = CreateHandler();

        // Recovery must not touch the database until Automate's startup migrations have run.
        var handleTask = handler.HandleAsync(new UmbracoApplicationStartedNotification(false), CancellationToken.None);
        var completedBeforeReady = await Task.WhenAny(handleTask, Task.Delay(TimeSpan.FromMilliseconds(200))) == handleTask;
        completedBeforeReady.ShouldBeFalse("Stuck run recovery ran before AutomateReadinessSignal was signalled.");
        (await _runRepository.GetAsync(stuckRun.Id))!.Status.ShouldBe(AutomationRunStatus.Running);

        _readinessSignal.Signal();
        await handleTask.WaitAsync(TimeSpan.FromSeconds(10));

        var recovered = await _runRepository.GetAsync(stuckRun.Id);
        recovered.ShouldNotBeNull();
        recovered.Status.ShouldBe(AutomationRunStatus.Failed);
        recovered.CompletedUtc.ShouldNotBeNull();
        recovered.Error.ShouldBe(InterruptedError);

        // The in-flight step is failed; the step that had already completed is left alone.
        var running = recovered.StepRuns.Single(s => s.StepId == stuckRun.StepRuns[1].StepId);
        running.Status.ShouldBe(StepRunStatus.Failed);
        running.Error.ShouldBe(InterruptedError);
        var completed = recovered.StepRuns.Single(s => s.StepId == stuckRun.StepRuns[0].StepId);
        completed.Status.ShouldBe(StepRunStatus.Completed);
        completed.Error.ShouldBeNull();
    }

    [Fact]
    public async Task HandleAsync_MarksPendingRunFailed()
    {
        var automation = await SaveAutomationAsync();
        var pendingRun = await SaveRunAsync(automation.Id, AutomationRunStatus.Pending, StepRunStatus.Pending);
        _readinessSignal.Signal();

        await CreateHandler().HandleAsync(new UmbracoApplicationStartedNotification(false), CancellationToken.None);

        var recovered = await _runRepository.GetAsync(pendingRun.Id);
        recovered!.Status.ShouldBe(AutomationRunStatus.Failed);
        recovered.StepRuns.Single().Status.ShouldBe(StepRunStatus.Failed);
    }

    [Theory]
    [InlineData(StepRunStatus.Sleeping)]
    [InlineData(StepRunStatus.WaitingForInput)]
    public async Task HandleAsync_LeavesDurablyWaitingRunForWorkflowCoreToResume(StepRunStatus durableStatus)
    {
        var automation = await SaveAutomationAsync();
        var waitingRun = await SaveRunAsync(automation.Id, AutomationRunStatus.Running, StepRunStatus.Completed, durableStatus);
        _readinessSignal.Signal();

        await CreateHandler().HandleAsync(new UmbracoApplicationStartedNotification(false), CancellationToken.None);

        var untouched = await _runRepository.GetAsync(waitingRun.Id);
        untouched!.Status.ShouldBe(AutomationRunStatus.Running);
        untouched.Error.ShouldBeNull();
        untouched.StepRuns.ShouldContain(s => s.Status == durableStatus);
    }

    [Fact]
    public async Task HandleAsync_FailsOrphanedStepOfCompletedRunWithoutChangingTheRun()
    {
        var automation = await SaveAutomationAsync();
        var completedRun = await SaveRunAsync(automation.Id, AutomationRunStatus.Completed, StepRunStatus.Running);
        _readinessSignal.Signal();

        await CreateHandler().HandleAsync(new UmbracoApplicationStartedNotification(false), CancellationToken.None);

        var run = await _runRepository.GetAsync(completedRun.Id);
        run!.Status.ShouldBe(AutomationRunStatus.Completed);
        run.Error.ShouldBeNull();
        var step = run.StepRuns.Single();
        step.Status.ShouldBe(StepRunStatus.Failed);
        step.Error.ShouldBe("Recovered after application restart — parent run already completed");
    }

    [Fact]
    public async Task HandleAsync_OnSubscriber_LeavesStuckRunAlone()
    {
        _serverRoleAccessor.Setup(r => r.CurrentServerRole).Returns(ServerRole.Subscriber);
        var automation = await SaveAutomationAsync();
        var stuckRun = await SaveRunAsync(automation.Id, AutomationRunStatus.Running, StepRunStatus.Running);
        _readinessSignal.Signal();

        await CreateHandler().HandleAsync(new UmbracoApplicationStartedNotification(false), CancellationToken.None);

        // The run may still be executing on the scheduling publisher.
        (await _runRepository.GetAsync(stuckRun.Id))!.Status.ShouldBe(AutomationRunStatus.Running);
    }

    private StuckRunRecoveryNotificationHandler CreateHandler()
        => new(
            _dbContextFactory,
            _serverRoleAccessor.Object,
            _readinessSignal,
            Mock.Of<ILogger<StuckRunRecoveryNotificationHandler>>());

    private async Task<Core.Automations.Automation> SaveAutomationAsync()
    {
        var automation = new AutomationBuilder().WithName("Recovery").Build();
        await _automationRepository.SaveAsync(automation);
        return automation;
    }

    private async Task<AutomationRun> SaveRunAsync(Guid automationId, AutomationRunStatus status, params StepRunStatus[] stepStatuses)
    {
        var run = await _runRepository.SaveAsync(new AutomationRunBuilder()
            .WithAutomationId(automationId)
            .WithStatus(status)
            .Build());

        foreach (var stepStatus in stepStatuses)
        {
            run.StepRuns.Add(await _runRepository.AddStepRunAsync(new StepRun
            {
                RunId = run.Id,
                StepId = Guid.NewGuid(),
                ActionAlias = "umbracoAutomate.logMessage",
                Status = stepStatus,
                StartedUtc = DateTime.UtcNow,
            }));
        }

        return run;
    }

    private static AutomationFactory CreateFactory()
    {
        var serializer = new EditableModelSerializer(
            Mock.Of<ISensitiveFieldProtector>(p => p.IsProtected(It.IsAny<string>()) == false),
            new ConfigurationReferenceResolver(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()));

        return new AutomationFactory(
            serializer,
            new ActionCollection(Array.Empty<IAction>),
            new TriggerCollection(Array.Empty<ITrigger>),
            new WebhookAuthenticatorCollection(Array.Empty<IWebhookAuthenticator>));
    }

    public void Dispose()
    {
        _fixture.Dispose();
        GC.SuppressFinalize(this);
    }
}
