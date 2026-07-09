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
/// End-to-end checks that a completed ForEach loop leaves no per-iteration state behind:
/// iteration-scoped step outputs are pruned from the workflow data, and execution pointers
/// carry index-only iteration contexts (no item payloads). Collection items use a
/// distinctive sentinel and the body steps never echo them, so asserting the sentinel is
/// absent from the serialised pointers proves items are not persisted per pointer.
/// </summary>
public class ForEachIterationStateTests : IAsyncLifetime
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

        var modelResolver = new EditableModelResolver(configuration);

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
            return new IControlFlow[] { new ForEachControlFlow(deps) };
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

        var workspace = new WorkspaceBuilder().WithName("ForEach Iteration State Workspace").Build();
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
    public async Task SequentialForEach_CompletedRun_LeavesNoIterationState()
    {
        var instance = await RunForEachAutomationAsync(
            alias: "test-foreach-sequential-iteration-state",
            collection: "sentinel-alpha,sentinel-beta,sentinel-gamma",
            runParallel: false);

        AssertNoIterationState(instance, sentinel: "sentinel-");
    }

    [Fact]
    public async Task ParallelForEach_CompletedRun_LeavesNoIterationState()
    {
        var instance = await RunForEachAutomationAsync(
            alias: "test-foreach-parallel-iteration-state",
            collection: "psentinel-x,psentinel-y,psentinel-z",
            runParallel: true);

        AssertNoIterationState(instance, sentinel: "psentinel-");
    }

    [Fact]
    public async Task NestedSequentialForEach_ItemsResolveThroughCache_AndAllScopesPrune()
    {
        // Outer loop over two JSON arrays; inner loop over ${loop.item} — the inner
        // collection can only materialise by resolving the outer iteration's item, which
        // exercises the recursive cache path since branched contexts carry no items.
        var outerStep = new StepConfiguration
        {
            Id = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.forEach",
            Name = "Outer ForEach",
            Alias = "outerForEach",
            Settings = new Dictionary<string, object?>
            {
                ["collection"] = "[[\"nsent-a\",\"nsent-b\"],[\"nsent-c\",\"nsent-d\"]]",
                ["runParallel"] = false,
            },
        };
        var innerStep = new StepConfiguration
        {
            Id = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.forEach",
            Name = "Inner ForEach",
            Alias = "innerForEach",
            Settings = new Dictionary<string, object?>
            {
                ["collection"] = "${ loop.item }",
                ["runParallel"] = false,
            },
        };
        var log = new StepConfiguration
        {
            Id = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.logMessage",
            Name = "Nested Log",
            Alias = "nestedLog",
            Settings = new Dictionary<string, object?>
            {
                ["message"] = "n:${ loop.item }",
                ["logLevel"] = "Information",
            },
        };

        var automation = new AutomationBuilder()
            .WithAlias("test-foreach-nested-iteration-state")
            .WithName("Test Nested ForEach Iteration State")
            .WithManualTrigger()
            .AddStep(outerStep)
            .AddStep(innerStep)
            .AddStep(log)
            .WithTriggerConnection(outerStep.Id)
            .WithConnection(outerStep.Id, innerStep.Id)
            .WithConnection(innerStep.Id, log.Id)
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

        // Items resolved correctly per inner iteration, in depth-first order.
        var completed = await _runRepository.GetAsync(run.Id);
        var messages = completed!.StepRuns
            .Where(s => s.ActionAlias == "umbracoAutomate.logMessage")
            .OrderBy(s => s.StartedUtc)
            .Select(s =>
            {
                using var doc = JsonDocument.Parse(s.OutputData!);
                return doc.RootElement.GetProperty("message").GetString();
            })
            .ToList();
        messages.ShouldBe(new[] { "n:nsent-a", "n:nsent-b", "n:nsent-c", "n:nsent-d" });

        // All nested scopes pruned, and no item payloads persisted into pointers.
        AssertNoIterationState(instance, sentinel: "nsent-");
    }

    /// <summary>
    /// Builds and triggers a ManualTrigger → ForEach → LogA → LogB automation whose body
    /// steps reference loop.index and each other (never loop.item), then waits for the
    /// workflow instance to complete and returns it.
    /// </summary>
    private async Task<WorkflowInstance> RunForEachAutomationAsync(string alias, string collection, bool runParallel)
    {
        var forEachStep = new StepConfiguration
        {
            Id = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.forEach",
            Name = "ForEach",
            Alias = "stateForEach",
            Settings = new Dictionary<string, object?>
            {
                ["collection"] = collection,
                ["runParallel"] = runParallel,
            },
        };
        var logA = new StepConfiguration
        {
            Id = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.logMessage",
            Name = "State A",
            Alias = "stateLogA",
            Settings = new Dictionary<string, object?>
            {
                ["message"] = "idx-${ loop.index }",
                ["logLevel"] = "Information",
            },
        };
        var logB = new StepConfiguration
        {
            Id = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.logMessage",
            Name = "State B (reads A)",
            Alias = "stateLogB",
            Settings = new Dictionary<string, object?>
            {
                ["message"] = "saw:${ steps.stateLogA.message }",
                ["logLevel"] = "Information",
            },
        };

        var automation = new AutomationBuilder()
            .WithAlias(alias)
            .WithName(alias)
            .WithManualTrigger()
            .AddStep(forEachStep)
            .AddStep(logA)
            .AddStep(logB)
            .WithTriggerConnection(forEachStep.Id)
            .WithConnection(forEachStep.Id, logA.Id)
            .WithConnection(logA.Id, logB.Id)
            .Build();

        _automationServiceMock
            .Setup(s => s.GetAllAutomationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { automation });

        var triggerMessage = new TriggerEventMessage
        {
            TriggerAlias = "umbracoAutomate.manual",
            InitiatorType = "system",
        };
        var body = JsonSerializer.Serialize(triggerMessage, JsonOptions.Default);
        await _handler.HandleAsync(body, CancellationToken.None);

        var run = await WaitForRunAsync(automation.Id, TimeSpan.FromSeconds(15));
        return await WaitForWorkflowCompleteAsync(run, TimeSpan.FromSeconds(15));
    }

    private void AssertNoIterationState(WorkflowInstance instance, string sentinel)
    {
        var data = instance.Data.ShouldBeOfType<AutomationWorkflowData>();

        // Every iteration scope must have been pruned when its branch drained.
        data.IterationStepOutputs.ShouldBeEmpty();
        data.IterationLastCompletedStepId.ShouldBeEmpty();

        // Body-step pointers carry index-only iteration contexts — no item payloads.
        foreach (var pointer in instance.ExecutionPointers)
        {
            if (pointer.ContextItem is ForEachIterationContext iterationContext)
            {
                iterationContext.Item.ShouldBeNull();
            }
        }

        // Belt and braces: serialise the pointers the way the EF persistence provider
        // serialises the instance and prove no collection item leaked into any pointer.
        var pointersJson = Newtonsoft.Json.JsonConvert.SerializeObject(
            instance.ExecutionPointers,
            new Newtonsoft.Json.JsonSerializerSettings { TypeNameHandling = Newtonsoft.Json.TypeNameHandling.All });
        pointersJson.ShouldNotContain(sentinel);
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
