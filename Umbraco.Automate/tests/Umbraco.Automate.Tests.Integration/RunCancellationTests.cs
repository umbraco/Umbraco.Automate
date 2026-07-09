using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.Middleware;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Bindings;
using Umbraco.Automate.Core.Conditions;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.ControlFlow;
using Umbraco.Automate.Core.ControlFlow.BuiltIn;
using Umbraco.Automate.Core.Diagnostics;
using Umbraco.Automate.Core.Dispatch;
using Umbraco.Automate.Core.Dispatch.Authorization;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Execution.ControlFlow;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Automate.Core.Versioning;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Persistence.Runs;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Automate.Tests.Common.Fixtures;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models.Membership;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace Umbraco.Automate.Tests.Integration;

/// <summary>
/// Verifies that terminating a run stops workflow execution while the engine is actively
/// executing steps. WorkflowCore's <c>TerminateWorkflow</c> makes a single attempt to acquire
/// the per-workflow lock, which the executor holds for the duration of each execution pass —
/// so a terminate issued mid-run almost always fails silently unless the run is cooperatively
/// cancelled at the step boundary. The body action sleeps per iteration so the lock is
/// reliably held when the terminate is issued, mirroring a user cancelling a long loop.
/// </summary>
public class RunCancellationTests : IAsyncLifetime
{
    private const int CollectionSize = 150;

    private ServiceProvider _provider = null!;
    private IWorkflowHost _workflowHost = null!;
    private EfCoreTestFixture _fixture = null!;
    private TriggerEventHandler _handler = null!;
    private IAutomationRunRepository _runRepository = null!;
    private IAutomationRunService _runService = null!;
    private Automation _automation = null!;
    private Workspace _workspace = null!;

    public async Task InitializeAsync()
    {
        _fixture = new EfCoreTestFixture();
        var dbContextFactory = new TestDbContextFactory(_fixture.CreateContext);
        var configuration = new ConfigurationBuilder().Build();

        var modelResolver = new EditableModelResolver(configuration);

        var actions = new ActionCollection(() =>
        {
            var deps = new ActionInfrastructure(modelResolver);
            return new IAction[] { new SlowStepAction(deps) };
        });

        var triggers = new TriggerCollection(() =>
        {
            var deps = new TriggerInfrastructure(modelResolver);
            return new ITrigger[] { new ManualTrigger(deps) };
        });

        var controlFlow = new ControlFlowCollection(() =>
        {
            var deps = new ControlFlowInfrastructure(modelResolver);
            return new IControlFlow[] { new ForEachControlFlow(deps) };
        });

        var middlewareCollection = new ActionMiddlewareCollection(Array.Empty<IActionMiddleware>);

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddDebug());
        services.AddWorkflow();
        // Backs RunCancellationStepMiddleware's short-TTL run-status cache.
        services.AddMemoryCache();
        services.AddWorkflowStepMiddleware<RunCancellationStepMiddleware>();

        _runRepository = new EFCoreAutomationRunRepository(dbContextFactory);
        services.AddSingleton(_runRepository);

        services.AddSingleton(actions);
        services.AddSingleton(triggers);
        services.AddSingleton(controlFlow);
        services.AddSingleton(middlewareCollection);
        services.AddSingleton(new BindingEvaluator(new BindingFilterCollection(Array.Empty<IBindingFilter>)));
        services.AddSingleton<SettingsBindingResolver>();
        services.AddSingleton<ConditionEvaluator>();
        services.AddSingleton<ActionMiddlewarePipeline>();
        services.AddMetrics();
        services.AddSingleton<AutomateMetrics>();

        _workspace = new WorkspaceBuilder().WithName("Run Cancellation Workspace").Build();
        var workspaceService = new Mock<IWorkspaceService>();
        workspaceService.Setup(w => w.GetWorkspaceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_workspace);
        services.AddSingleton(workspaceService.Object);

        var serviceAccountResolver = new Mock<IWorkspaceServiceAccountResolver>();
        serviceAccountResolver.Setup(r => r.GetServiceAccountAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<IUser>(u => u.AllowedSections == new[] { "content", "media", "members", "users" }));
        services.AddSingleton(serviceAccountResolver.Object);
        services.AddSingleton<ISectionAccessChecker, SectionAccessChecker>();

        services.AddSingleton(new TriggerDispatchAuthorizerCollection(Array.Empty<ITriggerDispatchAuthorizer>));

        services.AddSingleton(Mock.Of<IConnectionService>());

        services.Configure<RateLimitingOptions>(o => o.Enabled = false);
        services.AddSingleton<IRateLimitService, RateLimitService>();

        services.AddSingleton<IStepErrorClassifier, DefaultStepErrorClassifier>();
        services.AddSingleton<ForEachCollectionCache>();
        services.AddSingleton<StepOutputHydrationCache>();
        services.AddSingleton<IWorkflowCompiler, WorkflowCompiler>();
        services.AddSingleton<ICircuitBreakerService, StubCircuitBreakerService>();
        services.AddSingleton<IEventAggregator>(Mock.Of<IEventAggregator>());
        services.AddSingleton<IAutomationExecutor, AutomationExecutor>();

        _provider = services.BuildServiceProvider();

        _workflowHost = _provider.GetRequiredService<IWorkflowHost>();
        await _workflowHost.StartAsync(CancellationToken.None);

        _runService = new AutomationRunService(
            _runRepository,
            _workflowHost,
            _provider.GetRequiredService<IEventAggregator>(),
            _provider.GetRequiredService<ILogger<AutomationRunService>>());

        // Build: ManualTrigger → ForEach(150 items, sequential) → SlowStep
        var collection = string.Join(",", Enumerable.Range(0, CollectionSize).Select(i => $"item{i}"));
        var forEachStep = new StepConfiguration
        {
            Id = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.forEach",
            Name = "ForEach",
            Alias = "forEach",
            Settings = new Dictionary<string, object?>
            {
                ["collection"] = collection,
                ["runParallel"] = false,
            },
        };
        var slowStep = new StepConfiguration
        {
            Id = Guid.NewGuid(),
            ActionAlias = SlowStepAction.StepAlias,
            Name = "Slow Step",
            Alias = "slowStep",
            Settings = new Dictionary<string, object?>(),
        };

        _automation = new AutomationBuilder()
            .WithAlias("test-run-cancellation")
            .WithName("Test Run Cancellation")
            .WithManualTrigger()
            .AddStep(forEachStep)
            .AddStep(slowStep)
            .WithTriggerConnection(forEachStep.Id)
            .WithConnection(forEachStep.Id, slowStep.Id)
            .Build();

        var automationService = new Mock<IAutomationService>();
        automationService.Setup(s => s.GetAllAutomationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { _automation });

        var versionService = new Mock<IEntityVersionService>();

        var nodeEligibility = new Mock<IExecutionNodeEligibility>();
        nodeEligibility.Setup(e => e.CanExecuteWorkflows()).Returns(true);

        _handler = new TriggerEventHandler(
            automationService.Object,
            versionService.Object,
            _provider.GetRequiredService<IAutomationExecutor>(),
            nodeEligibility.Object,
            triggers,
            _provider.GetRequiredService<IWorkspaceServiceAccountResolver>(),
            _provider.GetRequiredService<ISectionAccessChecker>(),
            _provider.GetRequiredService<TriggerDispatchAuthorizerCollection>(),
            _provider.GetRequiredService<IOptionsMonitor<ExecutionOptions>>(),
            _provider.GetRequiredService<ILogger<TriggerEventHandler>>());
    }

    [Fact]
    public async Task TerminateRun_WhileActivelyExecuting_StopsExecution()
    {
        var triggerMessage = new TriggerEventMessage
        {
            TriggerAlias = "umbracoAutomate.manual",
            InitiatorType = "system",
        };
        var body = JsonSerializer.Serialize(triggerMessage, JsonOptions.Default);
        await _handler.HandleAsync(body, CancellationToken.None);

        // Wait until the run is actively executing: workflow instance assigned and a few
        // body iterations already recorded, so the terminate lands mid-loop.
        var run = await WaitForActiveExecutionAsync(minBodySteps: 3, TimeSpan.FromSeconds(30));

        var result = await _runService.TerminateRunAsync(run.Id);
        result.ShouldBe(RunLifecycleResult.Success);

        // The engine should stop promptly: the workflow instance must leave Runnable via
        // Terminated — not run the remaining ~145 iterations to completion.
        var instance = await WaitForWorkflowToStopAsync(run.WorkflowInstanceId!, TimeSpan.FromSeconds(30));
        instance.Status.ShouldBe(WorkflowStatus.Terminated);

        var finalRun = await _runRepository.GetAsync(run.Id);
        finalRun.ShouldNotBeNull();
        finalRun.Status.ShouldBe(AutomationRunStatus.Cancelled);
        finalRun.StepRuns
            .Count(s => s.ActionAlias == SlowStepAction.StepAlias)
            .ShouldBeLessThan(CollectionSize / 2);
    }

    private async Task<AutomationRun> WaitForActiveExecutionAsync(int minBodySteps, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var paged = await _runRepository.GetPagedByAutomationAsync(_automation.Id);
            var candidate = paged.Items.FirstOrDefault();
            if (candidate is not null)
            {
                var run = await _runRepository.GetAsync(candidate.Id);
                if (run is not null
                    && !string.IsNullOrEmpty(run.WorkflowInstanceId)
                    && run.StepRuns.Count(s => s.ActionAlias == SlowStepAction.StepAlias) >= minBodySteps)
                {
                    return run;
                }
            }

            await Task.Delay(50);
        }

        throw new TimeoutException($"Run did not reach {minBodySteps} executed body steps within {timeout}.");
    }

    private async Task<WorkflowInstance> WaitForWorkflowToStopAsync(string workflowInstanceId, TimeSpan timeout)
    {
        var persistence = _provider.GetRequiredService<IPersistenceProvider>();
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var instance = await persistence.GetWorkflowInstance(workflowInstanceId, CancellationToken.None);
            if (instance.Status != WorkflowStatus.Runnable)
            {
                return instance;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Workflow {workflowInstanceId} still runnable after {timeout}.");
    }

    public async Task DisposeAsync()
    {
        await _workflowHost.StopAsync(CancellationToken.None);
        await _provider.DisposeAsync();
        _fixture.Dispose();
    }
}

/// <summary>
/// Test action that sleeps long enough per invocation that the WorkflowCore executor is
/// reliably mid-pass (holding the workflow lock) whenever a terminate is issued.
/// </summary>
[Action(StepAlias, "Slow Step", Description = "Sleeps briefly.", Group = "Test")]
public sealed class SlowStepAction : ActionBase<SlowStepSettings, SlowStepOutput>
{
    public const string StepAlias = "test.slowStep";

    public SlowStepAction(ActionInfrastructure infrastructure)
        : base(infrastructure)
    {
    }

    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        await Task.Delay(100, cancellationToken);
        return Success(new SlowStepOutput());
    }
}

/// <summary>Settings for <see cref="SlowStepAction"/> (none).</summary>
public sealed class SlowStepSettings
{
}

/// <summary>Output for <see cref="SlowStepAction"/> (none).</summary>
public sealed class SlowStepOutput
{
}
