using System.Collections.Concurrent;
using System.Reflection;
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
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.StepTypes;
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
using WorkflowCore.Models;

namespace Umbraco.Automate.Tests.Integration;

/// <summary>
/// A real WorkflowCore host and compiler (as in <see cref="ApprovalOutcomeTests"/>) that runs an
/// automation made of test actions to completion. It also records the latest <see cref="StepRun"/>
/// object each step handed to the run repository, because the persistence layer does not store
/// <see cref="StepRun.BranchOutcome"/> and a read-back would always show it empty.
/// </summary>
internal sealed class ActionRoutingHarness : IAsyncDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IWorkflowHost _workflowHost;
    private readonly IPersistenceProvider _persistence;
    private readonly EfCoreTestFixture _fixture;
    private readonly IAutomationRunRepository _runRepository;
    private readonly RecordingRepositoryProxy _recorder;
    private readonly Mock<IAutomationService> _automationService;
    private readonly TriggerEventHandler _handler;

    private ActionRoutingHarness(
        ServiceProvider provider,
        EfCoreTestFixture fixture,
        IAutomationRunRepository runRepository,
        RecordingRepositoryProxy recorder,
        TriggerEventHandler handler,
        Mock<IAutomationService> automationService)
    {
        _provider = provider;
        _workflowHost = provider.GetRequiredService<IWorkflowHost>();
        _persistence = provider.GetRequiredService<IPersistenceProvider>();
        _fixture = fixture;
        _runRepository = runRepository;
        _recorder = recorder;
        _handler = handler;
        _automationService = automationService;
    }

    public static async Task<ActionRoutingHarness> StartAsync(
        Func<ActionInfrastructure, IEnumerable<IAction>> testActions)
    {
        var fixture = new EfCoreTestFixture();
        var dbContextFactory = new TestDbContextFactory(fixture.CreateContext);
        var modelResolver = new EditableModelResolver(new ConfigurationReferenceResolver(new ConfigurationBuilder().Build()));

        var actions = new ActionCollection(() =>
        {
            var deps = new ActionInfrastructure(modelResolver);
            return new IAction[] { new LogMessageAction(deps, LoggerFactory.Create(b => b.AddDebug()).CreateLogger<LogMessageAction>()) }
                .Concat(testActions(deps));
        });

        var triggers = new TriggerCollection(() => new ITrigger[] { new ManualTrigger(new TriggerInfrastructure(modelResolver)) });

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddDebug());
        services.AddWorkflow();

        IAutomationRunRepository runRepository = new EFCoreAutomationRunRepository(dbContextFactory);
        var recordingRepository = DispatchProxy.Create<IAutomationRunRepository, RecordingRepositoryProxy>();
        var recorder = (RecordingRepositoryProxy)(object)recordingRepository;
        recorder.Inner = runRepository;
        services.AddSingleton(recordingRepository);

        services.AddSingleton(actions);
        services.AddSingleton(triggers);
        services.AddSingleton(new ControlFlowCollection(Array.Empty<IControlFlow>));
        services.AddSingleton(new ActionMiddlewareCollection(Array.Empty<IActionMiddleware>));
        services.AddSingleton(new BindingEvaluator(new BindingFilterCollection(Array.Empty<IBindingFilter>)));
        services.AddSingleton<ForEachCollectionCache>();
        services.AddSingleton<StepOutputHydrationCache>();
        services.AddSingleton<SettingsBindingResolver>();
        services.AddSingleton<ConditionEvaluator>();
        services.AddSingleton<ActionMiddlewarePipeline>();
        services.AddMetrics();
        services.AddSingleton<AutomateMetrics>();

        var workspace = new WorkspaceBuilder().WithName("Action Outcome Workspace").Build();
        var workspaceService = new Mock<IWorkspaceService>();
        workspaceService.Setup(w => w.GetWorkspaceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(workspace);
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

        var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<IWorkflowHost>().StartAsync(CancellationToken.None);

        var automationService = new Mock<IAutomationService>();
        var nodeEligibility = new Mock<IExecutionNodeEligibility>();
        nodeEligibility.Setup(e => e.CanExecuteWorkflows()).Returns(true);

        var handler = new TriggerEventHandler(
            automationService.Object,
            Mock.Of<IEntityVersionService>(),
            provider.GetRequiredService<IAutomationExecutor>(),
            nodeEligibility.Object,
            triggers,
            provider.GetRequiredService<IWorkspaceServiceAccountResolver>(),
            provider.GetRequiredService<ISectionAccessChecker>(),
            provider.GetRequiredService<TriggerDispatchAuthorizerCollection>(),
            provider.GetRequiredService<IOptionsMonitor<ExecutionOptions>>(),
            provider.GetRequiredService<ILogger<TriggerEventHandler>>());

        return new ActionRoutingHarness(provider, fixture, runRepository, recorder, handler, automationService);
    }

    /// <summary>Starts the automation from its manual trigger and waits for the workflow to finish.</summary>
    public async Task<ActionRoutingRun> RunToCompletionAsync(Automation automation, Dictionary<string, object?>? triggerOutput = null)
    {
        _automationService
            .Setup(s => s.GetAllAutomationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { automation });

        var message = new TriggerEventMessage { TriggerAlias = "umbracoAutomate.manual", InitiatorType = "system" };
        message.OutputData = triggerOutput is null ? null : JsonSerializer.Serialize(triggerOutput, JsonOptions.Default);
        await _handler.HandleAsync(JsonSerializer.Serialize(message, JsonOptions.Default), CancellationToken.None);

        var deadline = DateTime.UtcNow + TestTimeouts.WorkflowWait;
        while (DateTime.UtcNow < deadline)
        {
            var run = (await _runRepository.GetPagedByAutomationAsync(automation.Id)).Items.FirstOrDefault();
            var instanceId = run is null ? null : (await _runRepository.GetAsync(run.Id))?.WorkflowInstanceId;
            if (!string.IsNullOrEmpty(instanceId)
                && await _persistence.GetWorkflowInstance(instanceId) is { Status: WorkflowStatus.Complete })
            {
                var completed = (await _runRepository.GetAsync(run!.Id))!;
                return new ActionRoutingRun(completed.StepRuns.ToList(), _recorder.Latest);
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"The workflow for automation {automation.Id} did not complete within {TestTimeouts.WorkflowWait}.");
    }

    public async ValueTask DisposeAsync()
    {
        await _workflowHost.StopAsync(CancellationToken.None);
        await _provider.DisposeAsync();
        _fixture.Dispose();
    }

    /// <summary>Passes every call through and remembers the step run objects saved through it.</summary>
    internal class RecordingRepositoryProxy : DispatchProxy
    {
        public IAutomationRunRepository Inner { get; set; } = null!;

        public ConcurrentDictionary<Guid, StepRun> Latest { get; } = new();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod!.Name is nameof(IAutomationRunRepository.AddStepRunAsync) or nameof(IAutomationRunRepository.UpdateStepRunAsync)
                && args?[0] is StepRun stepRun)
            {
                Latest[stepRun.Id] = stepRun;
            }

            try
            {
                return targetMethod.Invoke(Inner, args);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }
    }
}

/// <summary>What a finished run looked like: persisted step runs, and the step runs as the engine last saved them.</summary>
internal sealed record ActionRoutingRun(
    IReadOnlyList<StepRun> StepRuns,
    IReadOnlyDictionary<Guid, StepRun> SavedStepRuns)
{
    public IReadOnlyList<StepRun> For(Guid stepId) => StepRuns.Where(s => s.StepId == stepId).ToList();

    /// <summary>The branch outcome the step last saved. See <see cref="ActionRoutingHarness"/> for why this is not read back.</summary>
    public string? BranchOutcomeOf(Guid stepId)
        => SavedStepRuns.Values.Single(s => s.StepId == stepId).BranchOutcome;
}
