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
using Umbraco.Automate.Tests.Common.Fixtures;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models.Membership;
using WorkflowCore.Interface;

namespace Umbraco.Automate.Tests.Integration;

/// <summary>
/// End-to-end test: a Manual Trigger executes a Run Script action, and the script's returned
/// value is persisted as the step's output through the real execution pipeline.
/// </summary>
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
        automationService.Setup(s => s.GetAllAutomationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { _automation });

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

        var completedRun = await WaitForStepRunAsync(TimeSpan.FromSeconds(10));

        completedRun.StepRuns.ShouldNotBeEmpty();
        var stepRun = completedRun.StepRuns.First();
        stepRun.ActionAlias.ShouldBe("umbracoAutomate.runScript");
        stepRun.Status.ShouldBe(StepRunStatus.Completed);

        // The script's returned object is serialized as the step output.
        stepRun.OutputData.ShouldNotBeNull();
        using var doc = JsonDocument.Parse(stepRun.OutputData!);
        doc.RootElement.GetProperty("result").GetProperty("answer").GetInt32().ShouldBe(42);
    }

    private async Task<AutomationRun> WaitForStepRunAsync(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var paged = await _runRepository.GetPagedByAutomationAsync(_automation.Id);
            if (paged.Items.FirstOrDefault() is { } run)
            {
                var full = await _runRepository.GetAsync(run.Id);
                if (full?.StepRuns.Count > 0 && full.StepRuns.All(s => s.Status != StepRunStatus.Running))
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
