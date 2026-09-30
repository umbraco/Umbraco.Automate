using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Actions.Middleware;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Bindings;
using Umbraco.Automate.Core.Conditions;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.ControlFlow;
using Umbraco.Automate.Core.Diagnostics;
using Umbraco.Automate.Core.Dispatch;
using Umbraco.Automate.Core.Dispatch.Authorization;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Execution.ControlFlow;
using Umbraco.Automate.Core.Messaging;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Scripting;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Automate.Core.Versioning;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Persistence.Runs;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Automate.Tests.Common;
using Umbraco.Automate.Tests.Common.Fixtures;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models.Membership;
using WorkflowCore.Interface;

namespace Umbraco.Automate.Tests.Integration;

/// <summary>
/// End-to-end tests for automation chaining: a Start Automation step in one automation's run
/// starts a real run of another automation through the same executor trigger dispatch uses,
/// carrying the origin chain so a cycle is rejected at run time.
/// </summary>
[Collection("WorkflowHost")]
public class StartAutomationChainingTests : IAsyncLifetime
{
    private const string StartAutomationAlias = "umbracoAutomate.startAutomation";
    private const string RunScriptAlias = "umbracoAutomate.runScript";

    private readonly Dictionary<Guid, Automation> _automations = [];
    private ServiceProvider _provider = null!;
    private IWorkflowHost _workflowHost = null!;
    private EfCoreTestFixture _fixture = null!;
    private TriggerEventHandler _handler = null!;
    private IAutomationRunRepository _runRepository = null!;
    private Automation? _manuallyTriggered;
    private Guid _workspaceId;

    public async Task InitializeAsync()
    {
        _fixture = new EfCoreTestFixture();
        var dbContextFactory = new TestDbContextFactory(_fixture.CreateContext);
        var configuration = new ConfigurationBuilder().Build();

        var modelResolver = new EditableModelResolver(new ConfigurationReferenceResolver(configuration));
        var loggerFactory = LoggerFactory.Create(b => b.AddDebug());

        var automationService = new Mock<IAutomationService>();
        // Only the automation under test answers the manual trigger; the chained one is reached
        // exclusively through the Start Automation step.
        automationService.Setup(s => s.GetAllAutomationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _manuallyTriggered is null ? [] : new[] { _manuallyTriggered });
        automationService.Setup(s => s.GetAutomationAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => _automations.GetValueOrDefault(id));

        // The collection is lazy, so the Start Automation action can take the real executor from
        // the provider built below.
        var actions = new ActionCollection(() =>
        {
            var deps = new ActionInfrastructure(modelResolver);
            return new IAction[]
            {
                new StartAutomationAction(
                    deps,
                    automationService.Object,
                    // No snapshots: the action falls back to the automation's current state.
                    Mock.Of<IEntityVersionService>(),
                    _provider.GetRequiredService<IAutomationExecutor>(),
                    _provider.GetRequiredService<IOptionsMonitor<ExecutionOptions>>(),
                    loggerFactory.CreateLogger<StartAutomationAction>()),
                new RunScriptAction(
                    deps,
                    new ScriptExecutor(Mock.Of<IHttpClientFactory>(), loggerFactory.CreateLogger<ScriptExecutor>()),
                    new ScriptValidator(),
                    Options.Create(new ScriptingOptions()),
                    Options.Create(new ExecutionOptions()),
                    loggerFactory.CreateLogger<RunScriptAction>()),
            };
        });

        var triggers = new TriggerCollection(() =>
        {
            var deps = new TriggerInfrastructure(modelResolver);
            return new ITrigger[] { new ManualTrigger(deps) };
        });

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddDebug());
        services.AddWorkflow();

        _runRepository = new EFCoreAutomationRunRepository(dbContextFactory);
        services.AddSingleton(_runRepository);

        services.AddSingleton(actions);
        services.AddSingleton(triggers);
        services.AddSingleton(new ActionMiddlewareCollection(Array.Empty<IActionMiddleware>));
        services.AddSingleton(new ControlFlowCollection(Enumerable.Empty<IControlFlow>));
        services.AddSingleton(new BindingEvaluator(new BindingFilterCollection(Array.Empty<IBindingFilter>)));
        services.AddSingleton<ForEachCollectionCache>();
        services.AddSingleton<StepOutputHydrationCache>();
        services.AddSingleton<SettingsBindingResolver>();
        services.AddSingleton<ConditionEvaluator>();
        services.AddSingleton<ActionMiddlewarePipeline>();
        services.AddMetrics();
        services.AddSingleton<AutomateMetrics>();

        var workspace = new WorkspaceBuilder().WithName("Chaining Test Workspace").Build();
        _workspaceId = workspace.Id;
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
        await _workflowHost.StartAsync(CancellationToken.None);

        var nodeEligibility = new Mock<IExecutionNodeEligibility>();
        nodeEligibility.Setup(e => e.CanExecuteWorkflows()).Returns(true);

        _handler = new TriggerEventHandler(
            automationService.Object,
            Mock.Of<IEntityVersionService>(),
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
    public async Task StartAutomationStep_StartsChildRunAsSystemWithOriginChain()
    {
        var child = BuildAutomation(
            "child",
            new StepConfigurationBuilder()
                .WithActionAlias(RunScriptAlias)
                .WithName("Echo")
                .WithAlias("echo")
                .WithSetting("script", "export default function (data) { return data.trigger.greeting; }")
                .Build());

        var startChild = new StepConfigurationBuilder()
            .WithActionAlias(StartAutomationAlias)
            .WithName("Start child")
            .WithAlias("startChild")
            .WithSetting("automationKey", child.Id.ToString())
            .WithSetting("triggerData", """{ "greeting": "hello from the parent" }""")
            .Build();
        var parent = BuildAutomation("parent", startChild);
        _manuallyTriggered = parent;

        await TriggerManuallyAsync();

        var parentRun = await WaitForRunAsync(parent.Id, expectedStepRuns: 1);
        var startStep = parentRun.StepRuns.Single();
        startStep.Status.ShouldBe(StepRunStatus.Completed, startStep.Error);

        var childRun = await WaitForRunAsync(child.Id, expectedStepRuns: 1);

        // The step's output names the child run it started.
        using (var output = JsonDocument.Parse(startStep.OutputData!))
        {
            output.RootElement.GetProperty("started").GetBoolean().ShouldBeTrue();
            output.RootElement.GetProperty("runId").GetGuid().ShouldBe(childRun.Id);
        }

        // Started imperatively, so the child is a system run correlated to the parent run.
        childRun.InitiatedBy.ShouldBe(TriggerInitiatorType.System);
        childRun.CorrelationId.ShouldBe(parentRun.Id.ToString());

        // The configured trigger data reached the child as its trigger output.
        var echo = childRun.StepRuns.Single();
        echo.Status.ShouldBe(StepRunStatus.Completed, echo.Error);
        using (var echoOutput = JsonDocument.Parse(echo.OutputData!))
        {
            echoOutput.RootElement.GetProperty("result").GetString().ShouldBe("hello from the parent");
        }

        // The origin chain is not a run column; it travels in the workflow's execution context,
        // which is what a grandchild Start Automation step would read.
        childRun.WorkflowInstanceId.ShouldNotBeNull();
        var instance = await _provider.GetRequiredService<IPersistenceProvider>()
            .GetWorkflowInstance(childRun.WorkflowInstanceId!);
        var data = instance.Data.ShouldBeOfType<AutomationWorkflowData>();
        data.ExecutionContext.ShouldNotBeNull();
        data.ExecutionContext!.OriginChain.ShouldBe([parent.Id]);
    }

    [Fact]
    public async Task StartAutomationStep_CycleBackToAnAncestor_IsRejectedAtRunTime()
    {
        // Publish-time validation only rejects an automation starting itself directly; a cycle
        // through another automation (A -> B -> A) can only be caught when the run reaches it.
        var secondId = Guid.NewGuid();

        var a = BuildAutomation(
            "cycle-a",
            new StepConfigurationBuilder()
                .WithActionAlias(StartAutomationAlias)
                .WithName("Start B")
                .WithSetting("automationKey", secondId.ToString())
                .Build());
        var b = BuildAutomation(
            "cycle-b",
            new StepConfigurationBuilder()
                .WithActionAlias(StartAutomationAlias)
                .WithName("Start A")
                .WithSetting("automationKey", a.Id.ToString())
                .Build(),
            id: secondId);
        _manuallyTriggered = a;

        await TriggerManuallyAsync();

        var aRun = await WaitForRunAsync(a.Id, expectedStepRuns: 1);
        aRun.StepRuns.Single().Status.ShouldBe(StepRunStatus.Completed, aRun.StepRuns.Single().Error);

        var bRun = await WaitForRunAsync(b.Id, expectedStepRuns: 1);
        var startA = bRun.StepRuns.Single();
        startA.Status.ShouldBe(StepRunStatus.Failed);
        startA.ErrorCategory.ShouldBe(StepRunErrorCategory.ConfigurationError);
        startA.Error.ShouldNotBeNull();
        startA.Error.ShouldContain("would create a cycle");

        // A was not started a second time.
        var aRuns = await _runRepository.GetPagedByAutomationAsync(a.Id);
        aRuns.Items.Count().ShouldBe(1);
    }

    private Automation BuildAutomation(string alias, StepConfiguration step, Guid? id = null)
    {
        var automation = new AutomationBuilder()
            .WithId(id ?? Guid.NewGuid())
            .WithAlias($"test-chaining-{alias}")
            .WithName($"Chaining {alias}")
            .WithWorkspaceId(_workspaceId)
            .WithManualTrigger()
            .AddStep(step)
            .WithTriggerConnection(step.Id)
            .Build();

        _automations[automation.Id] = automation;
        return automation;
    }

    private async Task TriggerManuallyAsync()
    {
        var triggerMessage = new TriggerEventMessage
        {
            TriggerAlias = "umbracoAutomate.manual",
            InitiatorType = "user",
        };

        await _handler.HandleAsync(JsonSerializer.Serialize(triggerMessage, JsonOptions.Default), CancellationToken.None);
    }

    private async Task<AutomationRun> WaitForRunAsync(Guid automationId, int expectedStepRuns)
    {
        var timeout = TestTimeouts.WorkflowWait;
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var paged = await _runRepository.GetPagedByAutomationAsync(automationId);
            if (paged.Items.FirstOrDefault() is { } run)
            {
                var full = await _runRepository.GetAsync(run.Id);
                if (full?.StepRuns.Count >= expectedStepRuns
                    && full.StepRuns.All(s => s.Status != StepRunStatus.Running)
                    && full.WorkflowInstanceId is not null)
                {
                    return full;
                }
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Run of automation {automationId} did not complete within {timeout}.");
    }

    public async Task DisposeAsync()
    {
        await _workflowHost.StopAsync(CancellationToken.None);
        await _provider.DisposeAsync();
        _fixture.Dispose();
    }
}
