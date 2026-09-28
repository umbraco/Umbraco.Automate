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
/// End-to-end test: a Manual Trigger executes a Run Script action, and the script's returned
/// value is persisted as the step's output through the real execution pipeline.
/// </summary>
[Collection("WorkflowHost")]
public class ManualTriggerRunScriptTests : IAsyncLifetime
{
    private ServiceProvider _provider = null!;
    private IWorkflowHost _workflowHost = null!;
    private EfCoreTestFixture _fixture = null!;
    private TriggerEventHandler _handler = null!;
    private IAutomationRunRepository _runRepository = null!;
    private Automation _automation = null!;

    public async Task InitializeAsync()
    {
        _fixture = new EfCoreTestFixture();
        var dbContextFactory = new TestDbContextFactory(_fixture.CreateContext);
        var configuration = new ConfigurationBuilder().Build();

        var modelResolver = new EditableModelResolver(new ConfigurationReferenceResolver(configuration));
        var loggerFactory = LoggerFactory.Create(b => b.AddDebug());

        var actions = new ActionCollection(() =>
        {
            var deps = new ActionInfrastructure(modelResolver);
            var executor = new ScriptExecutor(Mock.Of<IHttpClientFactory>(), loggerFactory.CreateLogger<ScriptExecutor>());
            return new IAction[]
            {
                new RunScriptAction(
                    deps,
                    executor,
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

        var workspace = new WorkspaceBuilder().WithName("Run Script Test Workspace").Build();
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

        _automation = new AutomationBuilder()
            .WithAlias("test-manual-runscript")
            .WithName("Test Manual Run Script")
            .WithManualTrigger()
            .AddStep("umbracoAutomate.runScript", "Run Script", new Dictionary<string, object?>
            {
                ["script"] = "export default function () { return { answer: 21 * 2 }; }",
            })
            .Build();

        var automationService = new Mock<IAutomationService>();
        // Resolved per call so a test can swap in its own automation before triggering.
        automationService.Setup(s => s.GetAllAutomationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new[] { _automation });

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
    public async Task ManualTrigger_RunScript_PersistsScriptResultAsStepOutput()
    {
        var triggerMessage = new TriggerEventMessage
        {
            TriggerAlias = "umbracoAutomate.manual",
            InitiatorType = "system",
        };

        await _handler.HandleAsync(JsonSerializer.Serialize(triggerMessage, JsonOptions.Default), CancellationToken.None);

        var completedRun = await WaitForStepRunAsync(TestTimeouts.WorkflowWait, expectedStepRuns: 1);

        completedRun.StepRuns.ShouldNotBeEmpty();
        var stepRun = completedRun.StepRuns.First();
        stepRun.ActionAlias.ShouldBe("umbracoAutomate.runScript");
        stepRun.Status.ShouldBe(StepRunStatus.Completed);

        // The script's returned object is serialized as the step output.
        stepRun.OutputData.ShouldNotBeNull();
        using var doc = JsonDocument.Parse(stepRun.OutputData!);
        doc.RootElement.GetProperty("result").GetProperty("answer").GetInt32().ShouldBe(42);
    }

    [Fact]
    public async Task RunScript_ReadsTriggerAndPreviousStepOutputsFromData()
    {
        // The script's `data` argument carries the binding context, so a script can read a prior
        // step's output at the same path a binding would (${ steps.first.result.answer }).
        var first = new StepConfigurationBuilder()
            .WithActionAlias("umbracoAutomate.runScript")
            .WithName("First")
            .WithAlias("first")
            .WithSetting("script", "export default function () { return { answer: 42, tags: ['a', 'b', 'c'] }; }")
            .Build();
        var second = new StepConfigurationBuilder()
            .WithActionAlias("umbracoAutomate.runScript")
            .WithName("Second")
            .WithAlias("second")
            .WithSetting(
                "script",
                "export default function (data) { return [data.trigger.name, data.steps.first.result.answer, data.previous.result.tags.length].join('|'); }")
            .Build();

        _automation = new AutomationBuilder()
            .WithAlias("test-manual-runscript-binding-context")
            .WithName("Test Run Script Binding Context")
            .WithManualTrigger()
            .AddStep(first)
            .AddStep(second)
            .WithTriggerConnection(first.Id)
            .WithConnection(first.Id, second.Id)
            .Build();

        var triggerMessage = new TriggerEventMessage
        {
            TriggerAlias = "umbracoAutomate.manual",
            InitiatorType = "system",
            OutputData = JsonSerializer.Serialize(new Dictionary<string, object?> { ["name"] = "Home" }, JsonOptions.Default),
        };

        await _handler.HandleAsync(JsonSerializer.Serialize(triggerMessage, JsonOptions.Default), CancellationToken.None);

        var completedRun = await WaitForStepRunAsync(TestTimeouts.WorkflowWait, expectedStepRuns: 2);

        var secondRun = completedRun.StepRuns.Single(s => s.StepId == second.Id);
        secondRun.Status.ShouldBe(StepRunStatus.Completed, secondRun.Error);
        using var doc = JsonDocument.Parse(secondRun.OutputData!);
        doc.RootElement.GetProperty("result").GetString().ShouldBe("Home|42|3");
    }

    [Fact]
    public async Task RunScript_DoesNotResolveBindingsInsideTheScriptBody()
    {
        // `${ }` in the script source is JavaScript, not an Automate binding: the Script field does
        // not support bindings, so a string that looks like one reaches the engine verbatim and a
        // template literal keeps its JS meaning. Values come in through `data` instead.
        const string script =
            "export default function (data) {\n" +
            "    return {\n" +
            "        literal: '${ trigger.name }',\n" +
            "        template: `Hello ${data.trigger.name}`,\n" +
            "    };\n" +
            "}";

        var step = new StepConfigurationBuilder()
            .WithActionAlias("umbracoAutomate.runScript")
            .WithName("No Bindings")
            .WithAlias("noBindings")
            .WithSetting("script", script)
            .Build();

        _automation = new AutomationBuilder()
            .WithAlias("test-manual-runscript-no-bindings")
            .WithName("Test Run Script No Bindings")
            .WithManualTrigger()
            .AddStep(step)
            .WithTriggerConnection(step.Id)
            .Build();

        var triggerMessage = new TriggerEventMessage
        {
            TriggerAlias = "umbracoAutomate.manual",
            InitiatorType = "system",
            OutputData = JsonSerializer.Serialize(new Dictionary<string, object?> { ["name"] = "Home" }, JsonOptions.Default),
        };

        await _handler.HandleAsync(JsonSerializer.Serialize(triggerMessage, JsonOptions.Default), CancellationToken.None);

        var completedRun = await WaitForStepRunAsync(TestTimeouts.WorkflowWait, expectedStepRuns: 1);

        var stepRun = completedRun.StepRuns.Single();
        stepRun.Status.ShouldBe(StepRunStatus.Completed, stepRun.Error);
        using var doc = JsonDocument.Parse(stepRun.OutputData!);
        var result = doc.RootElement.GetProperty("result");
        result.GetProperty("literal").GetString().ShouldBe("${ trigger.name }");
        result.GetProperty("template").GetString().ShouldBe("Hello Home");
    }

    private async Task<AutomationRun> WaitForStepRunAsync(TimeSpan timeout, int expectedStepRuns)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var paged = await _runRepository.GetPagedByAutomationAsync(_automation.Id);
            if (paged.Items.FirstOrDefault() is { } run)
            {
                var full = await _runRepository.GetAsync(run.Id);
                if (full?.StepRuns.Count >= expectedStepRuns && full.StepRuns.All(s => s.Status != StepRunStatus.Running))
                {
                    return full;
                }
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Step runs did not complete within {timeout}.");
    }

    public async Task DisposeAsync()
    {
        await _workflowHost.StopAsync(CancellationToken.None);
        await _provider.DisposeAsync();
        _fixture.Dispose();
    }
}
