using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.BuiltIn;
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
using Umbraco.Automate.Core.Messaging;
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
using Umbraco.Cms.Core.Services;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace Umbraco.Automate.Tests.Integration;

/// <summary>
/// End-to-end checks on the Parallel container's fan-out. WorkflowCore spawns one child pointer
/// per (branch value × child step) pair, so a container that branches once per child runs every
/// branch once per branch — three branches would log nine times. These tests run a real Parallel
/// automation and count what actually executed, which is the only place that mistake shows up:
/// the step body's return value on its own looks reasonable either way.
/// </summary>
public class ParallelContainerTests : IAsyncLifetime
{
    private ServiceProvider _provider = null!;
    private IWorkflowHost _workflowHost = null!;
    private EfCoreTestFixture _fixture = null!;
    private TriggerEventHandler _handler = null!;
    private IAutomationRunRepository _runRepository = null!;
    private IPersistenceProvider _persistence = null!;
    private Mock<IAutomationService> _automationServiceMock = null!;

    public async Task InitializeAsync()
    {
        _fixture = new EfCoreTestFixture();
        var dbContextFactory = new TestDbContextFactory(_fixture.CreateContext);
        var configuration = new ConfigurationBuilder().Build();

        var modelResolver = new EditableModelResolver(new ConfigurationReferenceResolver(configuration));

        var actions = new ActionCollection(() =>
        {
            var deps = new ActionInfrastructure(modelResolver);
            return new IAction[]
            {
                new LogMessageAction(deps, LoggerFactory.Create(b => b.AddDebug()).CreateLogger<LogMessageAction>()),
            };
        });

        var triggers = new TriggerCollection(() =>
        {
            var deps = new TriggerInfrastructure(modelResolver);
            return new ITrigger[] { new ManualTrigger(deps) };
        });

        var controlFlow = new ControlFlowCollection(() =>
        {
            var deps = new ControlFlowInfrastructure(modelResolver);
            return new IControlFlow[] { new ParallelControlFlow(deps) };
        });

        var middlewareCollection = new ActionMiddlewareCollection(Array.Empty<IActionMiddleware>);

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddDebug());
        services.AddWorkflow();

        _runRepository = new EFCoreAutomationRunRepository(dbContextFactory);
        services.AddSingleton(_runRepository);

        services.AddSingleton(actions);
        services.AddSingleton(triggers);
        services.AddSingleton(controlFlow);
        services.AddSingleton(middlewareCollection);
        services.AddSingleton(new BindingEvaluator(new BindingFilterCollection(Array.Empty<IBindingFilter>)));
        services.AddSingleton<ForEachCollectionCache>();
        services.AddSingleton<StepOutputHydrationCache>();
        services.AddSingleton<SettingsBindingResolver>();
        services.AddSingleton<ConditionEvaluator>();
        services.AddSingleton<ActionMiddlewarePipeline>();
        services.AddMetrics();
        services.AddSingleton<AutomateMetrics>();

        var workspace = new WorkspaceBuilder().WithName("Parallel Container Workspace").Build();
        var workspaceService = new Mock<IWorkspaceService>();
        workspaceService.Setup(w => w.GetWorkspaceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspace);
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
        services.AddSingleton<IWorkflowCompiler, WorkflowCompiler>();
        services.AddSingleton<ICircuitBreakerService, StubCircuitBreakerService>();
        services.AddSingleton<IEventAggregator>(Mock.Of<IEventAggregator>());
        services.AddSingleton<IAutomationExecutor, AutomationExecutor>();

        _provider = services.BuildServiceProvider();

        _workflowHost = _provider.GetRequiredService<IWorkflowHost>();
        _persistence = _provider.GetRequiredService<IPersistenceProvider>();
        await _workflowHost.StartAsync(CancellationToken.None);

        _automationServiceMock = new Mock<IAutomationService>();

        var versionService = new Mock<IEntityVersionService>();

        var nodeEligibility = new Mock<IExecutionNodeEligibility>();
        nodeEligibility.Setup(e => e.CanExecuteWorkflows()).Returns(true);

        _handler = new TriggerEventHandler(
            _automationServiceMock.Object,
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
    public async Task Parallel_WithThreeBranches_RunsEachBranchExactlyOnce()
    {
        var (automation, _) = await RunParallelAutomationAsync("test-parallel-fanout");

        var messages = await GetLogMessagesAsync(automation.Id);

        messages.Count(m => m == "branch-a").ShouldBe(1);
        messages.Count(m => m == "branch-b").ShouldBe(1);
        messages.Count(m => m == "branch-c").ShouldBe(1);
    }

    [Fact]
    public async Task Parallel_DoneHandle_RunsOnceAfterEveryBranch()
    {
        var (automation, _) = await RunParallelAutomationAsync("test-parallel-done-handle");

        var messages = await GetLogMessagesAsync(automation.Id);

        // Exactly one "after" and it is last — the container only fires its Done outcome
        // once IsBranchComplete reports the whole fan-out has drained.
        messages.Count(m => m == "after").ShouldBe(1);
        messages[^1].ShouldBe("after");
        messages.Count.ShouldBe(4);
    }

    [Fact]
    public async Task Parallel_CompletedRun_LeavesNoIterationState()
    {
        var (_, instance) = await RunParallelAutomationAsync("test-parallel-iteration-state");

        var data = instance.Data.ShouldBeOfType<AutomationWorkflowData>();
        data.IterationStepOutputs.ShouldBeEmpty();
        data.IterationLastCompletedStepId.ShouldBeEmpty();
    }

    /// <summary>
    /// Builds and triggers a ManualTrigger → Parallel automation with three body branches and
    /// one Done branch, then waits for the workflow instance to complete.
    /// </summary>
    private async Task<(Automation Automation, WorkflowInstance Instance)> RunParallelAutomationAsync(string alias)
    {
        var parallelStep = new StepConfiguration
        {
            Id = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.parallel",
            Name = "Parallel",
            Alias = "fanOut",
            Settings = new Dictionary<string, object?>(),
        };

        var branchA = LogStep("Branch A", "branchA", "branch-a");
        var branchB = LogStep("Branch B", "branchB", "branch-b");
        var branchC = LogStep("Branch C", "branchC", "branch-c");
        var after = LogStep("After", "afterParallel", "after");

        var automation = new AutomationBuilder()
            .WithAlias(alias)
            .WithName(alias)
            .WithManualTrigger()
            .AddStep(parallelStep)
            .AddStep(branchA)
            .AddStep(branchB)
            .AddStep(branchC)
            .AddStep(after)
            .WithTriggerConnection(parallelStep.Id)
            .WithConnection(parallelStep.Id, branchA.Id, sourceHandle: ContainerHandles.Body)
            .WithConnection(parallelStep.Id, branchB.Id, sourceHandle: ContainerHandles.Body)
            .WithConnection(parallelStep.Id, branchC.Id, sourceHandle: ContainerHandles.Body)
            .WithConnection(parallelStep.Id, after.Id, sourceHandle: ContainerHandles.Done)
            .Build();

        _automationServiceMock
            .Setup(s => s.GetAllAutomationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { automation });

        var triggerMessage = new TriggerEventMessage
        {
            TriggerAlias = "umbracoAutomate.manual",
            InitiatorType = "system",
        };
        await _handler.HandleAsync(JsonSerializer.Serialize(triggerMessage, JsonOptions.Default), CancellationToken.None);

        var run = await WaitForRunAsync(automation.Id, TimeSpan.FromSeconds(15));
        var instance = await WaitForWorkflowCompleteAsync(run, TimeSpan.FromSeconds(15));
        return (automation, instance);
    }

    private static StepConfiguration LogStep(string name, string stepAlias, string message) => new()
    {
        Id = Guid.NewGuid(),
        ActionAlias = "umbracoAutomate.logMessage",
        Name = name,
        Alias = stepAlias,
        Settings = new Dictionary<string, object?>
        {
            ["message"] = message,
            ["logLevel"] = "Information",
        },
    };

    private async Task<List<string?>> GetLogMessagesAsync(Guid automationId)
    {
        var paged = await _runRepository.GetPagedByAutomationAsync(automationId);
        var completed = await _runRepository.GetAsync(paged.Items.First().Id);

        return completed!.StepRuns
            .Where(s => s.ActionAlias == "umbracoAutomate.logMessage")
            .OrderBy(s => s.StartedUtc)
            .Select(s =>
            {
                using var doc = JsonDocument.Parse(s.OutputData!);
                return doc.RootElement.GetProperty("message").GetString();
            })
            .ToList();
    }

    private async Task<AutomationRun> WaitForRunAsync(Guid automationId, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var result = await _runRepository.GetPagedByAutomationAsync(automationId);
            if (result.Items.Any())
            {
                return result.Items.First();
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"No automation run found within {timeout}.");
    }

    private async Task<WorkflowInstance> WaitForWorkflowCompleteAsync(AutomationRun run, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            // WorkflowInstanceId is set once the executor has started the workflow.
            var refreshed = await _runRepository.GetAsync(run.Id);
            var workflowInstanceId = refreshed?.WorkflowInstanceId;
            if (!string.IsNullOrEmpty(workflowInstanceId))
            {
                var instance = await _persistence.GetWorkflowInstance(workflowInstanceId);
                if (instance is { Status: WorkflowStatus.Complete })
                {
                    return instance;
                }
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Workflow for run {run.Id} did not complete within {timeout}.");
    }

    public async Task DisposeAsync()
    {
        await _workflowHost.StopAsync(CancellationToken.None);
        await _provider.DisposeAsync();
        _fixture.Dispose();
    }
}
