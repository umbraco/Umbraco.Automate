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
/// End-to-end checks for large step-output reference-offload: outputs whose serialized size
/// exceeds <see cref="ExecutionOptions.MaxInlineOutputBytes"/> are kept out of the workflow
/// data (which WorkflowCore re-serializes on every execution pass) and replaced by a
/// <see cref="StepOutputReference"/> marker, while later steps binding into them still
/// resolve the real value by hydrating from the StepRun table — including inside ForEach
/// iteration scopes. Small outputs stay inline exactly as before, which also keeps the
/// pending-approvals prompt flow working.
/// </summary>
public class StepOutputOffloadTests : IAsyncLifetime
{
    /// <summary>Inline threshold used by these tests: big payloads offload, approval prompts stay inline.</summary>
    private const int MaxInlineBytes = 512;

    private const string PayloadSentinel = "OFFLOAD-SENTINEL-";

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
                new RequestApprovalAction(deps),
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

        // The threshold under test: anything above this many UTF-8 bytes is offloaded.
        services.Configure<ExecutionOptions>(o => o.MaxInlineOutputBytes = MaxInlineBytes);

        var workspace = new WorkspaceBuilder().WithName("Step Output Offload Workspace").Build();
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
    public async Task LargeOutput_IsOffloadedFromWorkflowData_AndResolvesInLaterStep()
    {
        var payload = PayloadSentinel + new string('x', 4000);
        var bigStep = LogStep("bigLog", payload);
        var readerStep = LogStep("readerLog", "saw:${ steps.bigLog.message }");

        var automation = BuildAutomation("test-offload-large-output", bigStep, readerStep);
        await TriggerAsync(automation);

        var run = await WaitForRunAsync(automation.Id, TimeSpan.FromSeconds(15));
        var instance = await WaitForWorkflowStatusAsync(run, WorkflowStatus.Complete, TimeSpan.FromSeconds(15));

        // The later step resolved the real value through the offloaded output.
        var completed = await _runRepository.GetAsync(run.Id);
        var readerRun = completed!.StepRuns.Single(s => s.StepId == readerStep.Id);
        ReadMessage(readerRun.OutputData!).ShouldBe("saw:" + payload);

        // The workflow data holds a marker in place of the large output...
        var data = instance.Data.ShouldBeOfType<AutomationWorkflowData>();
        StepOutputReference.TryGetStepRunId(data.StepOutputs[bigStep.Id], out var referencedStepRunId).ShouldBeTrue();
        referencedStepRunId.ShouldBe(completed.StepRuns.Single(s => s.StepId == bigStep.Id).Id);

        // ...and the blob the persistence provider would write contains no payload at all.
        var blob = SerializeAsPersistenceBlob(data);
        blob.ShouldNotContain(PayloadSentinel);
        blob.ShouldContain(StepOutputReference.MarkerKey);
    }

    [Fact]
    public async Task SmallOutput_StaysInlineInWorkflowData()
    {
        const string smallMessage = "tiny-inline-sentinel";
        var smallStep = LogStep("smallLog", smallMessage);
        var readerStep = LogStep("readerLog", "saw:${ steps.smallLog.message }");

        var automation = BuildAutomation("test-offload-small-output", smallStep, readerStep);
        await TriggerAsync(automation);

        var run = await WaitForRunAsync(automation.Id, TimeSpan.FromSeconds(15));
        var instance = await WaitForWorkflowStatusAsync(run, WorkflowStatus.Complete, TimeSpan.FromSeconds(15));

        var completed = await _runRepository.GetAsync(run.Id);
        var readerRun = completed!.StepRuns.Single(s => s.StepId == readerStep.Id);
        ReadMessage(readerRun.OutputData!).ShouldBe("saw:" + smallMessage);

        // Inline exactly as before this feature: value present in the blob, no markers anywhere.
        var data = instance.Data.ShouldBeOfType<AutomationWorkflowData>();
        data.StepOutputs[smallStep.Id]["message"].ShouldBe(smallMessage);

        var blob = SerializeAsPersistenceBlob(data);
        blob.ShouldContain(smallMessage);
        blob.ShouldNotContain(StepOutputReference.MarkerKey);
    }

    [Fact]
    public async Task LargeOutput_InsideForEachIteration_ResolvesInSiblingStep()
    {
        var payload = PayloadSentinel + new string('y', 4000);
        var forEachStep = new StepConfiguration
        {
            Id = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.forEach",
            Name = "ForEach",
            Alias = "offloadForEach",
            Settings = new Dictionary<string, object?>
            {
                ["collection"] = "alpha,beta",
                ["runParallel"] = false,
            },
        };
        var bigStep = LogStep("bigIterLog", payload + "|${ loop.item }");
        var readerStep = LogStep("iterReaderLog", "saw:${ steps.bigIterLog.message }");

        var automation = new AutomationBuilder()
            .WithAlias("test-offload-foreach-iteration")
            .WithName("Test Offload Inside ForEach")
            .WithManualTrigger()
            .AddStep(forEachStep)
            .AddStep(bigStep)
            .AddStep(readerStep)
            .WithTriggerConnection(forEachStep.Id)
            .WithConnection(forEachStep.Id, bigStep.Id)
            .WithConnection(bigStep.Id, readerStep.Id)
            .Build();
        await TriggerAsync(automation);

        var run = await WaitForRunAsync(automation.Id, TimeSpan.FromSeconds(15));
        var instance = await WaitForWorkflowStatusAsync(run, WorkflowStatus.Complete, TimeSpan.FromSeconds(15));

        // Each iteration's reader saw its own iteration's offloaded output.
        var completed = await _runRepository.GetAsync(run.Id);
        var messages = completed!.StepRuns
            .Where(s => s.StepId == readerStep.Id)
            .OrderBy(s => s.StartedUtc)
            .Select(s => ReadMessage(s.OutputData!))
            .ToList();
        messages.ShouldBe(new[] { $"saw:{payload}|alpha", $"saw:{payload}|beta" });

        // No payload persisted in the workflow data blob.
        var data = instance.Data.ShouldBeOfType<AutomationWorkflowData>();
        var blob = SerializeAsPersistenceBlob(data);
        blob.ShouldNotContain(PayloadSentinel);
    }

    [Fact]
    public async Task ApprovalPrompt_StillReadable_WhenLargeOutputsAreOffloaded()
    {
        const string prompt = "Please approve the release";
        var payload = PayloadSentinel + new string('z', 4000);
        var bigStep = LogStep("bigLog", payload);
        var approvalStep = new StepConfiguration
        {
            Id = Guid.NewGuid(),
            ActionAlias = RequestApprovalAction.ApprovalActionAlias,
            Name = "Approval",
            Alias = "approval",
            Settings = new Dictionary<string, object?> { ["prompt"] = prompt },
        };

        var automation = BuildAutomation("test-offload-approval-prompt", bigStep, approvalStep);
        await TriggerAsync(automation);

        var run = await WaitForRunAsync(automation.Id, TimeSpan.FromSeconds(15));

        // The pending-approvals API reads the prompt from StepRun.OutputData — which always
        // holds the full output regardless of offloading. (The workflow instance itself stays
        // Runnable while waiting for the approval event, so wait on the step run instead.)
        var approvalRun = await WaitForStepRunStatusAsync(run, approvalStep.Id, StepRunStatus.WaitingForInput, TimeSpan.FromSeconds(15));
        using (var doc = JsonDocument.Parse(approvalRun.OutputData!))
        {
            doc.RootElement.GetProperty("prompt").GetString().ShouldBe(prompt);
        }

        // The approval output is tiny and stays inline; only the big output is offloaded.
        var refreshed = await _runRepository.GetAsync(run.Id);
        var instance = await _persistence.GetWorkflowInstance(refreshed!.WorkflowInstanceId!);
        var data = instance.Data.ShouldBeOfType<AutomationWorkflowData>();
        data.StepOutputs[approvalStep.Id]["prompt"].ShouldBe(prompt);
        StepOutputReference.TryGetStepRunId(data.StepOutputs[bigStep.Id], out _).ShouldBeTrue();
    }

    private static StepConfiguration LogStep(string alias, string message) => new()
    {
        Id = Guid.NewGuid(),
        ActionAlias = "umbracoAutomate.logMessage",
        Name = alias,
        Alias = alias,
        Settings = new Dictionary<string, object?>
        {
            ["message"] = message,
            ["logLevel"] = "Information",
        },
    };

    private static Automation BuildAutomation(string alias, StepConfiguration first, StepConfiguration second)
        => new AutomationBuilder()
            .WithAlias(alias)
            .WithName(alias)
            .WithManualTrigger()
            .AddStep(first)
            .AddStep(second)
            .WithTriggerConnection(first.Id)
            .WithConnection(first.Id, second.Id)
            .Build();

    private async Task TriggerAsync(Automation automation)
    {
        _automationServiceMock
            .Setup(s => s.GetAllAutomationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { automation });

        var triggerMessage = new TriggerEventMessage
        {
            TriggerAlias = "umbracoAutomate.manual",
            InitiatorType = "system",
        };
        await _handler.HandleAsync(JsonSerializer.Serialize(triggerMessage, JsonOptions.Default), CancellationToken.None);
    }

    private static string ReadMessage(string outputData)
    {
        using var doc = JsonDocument.Parse(outputData);
        return doc.RootElement.GetProperty("message").GetString()!;
    }

    /// <summary>
    /// Serializes workflow data the way <c>EFCoreWorkflowPersistenceProvider</c> serializes
    /// the instance blob, to prove what would (not) hit the database on every persist.
    /// </summary>
    private static string SerializeAsPersistenceBlob(AutomationWorkflowData data)
        => Newtonsoft.Json.JsonConvert.SerializeObject(
            data,
            new Newtonsoft.Json.JsonSerializerSettings
            {
                TypeNameHandling = Newtonsoft.Json.TypeNameHandling.All,
                ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore,
            });

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

    private async Task<StepRun> WaitForStepRunStatusAsync(AutomationRun run, Guid stepId, StepRunStatus status, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var refreshed = await _runRepository.GetAsync(run.Id);
            var stepRun = refreshed?.StepRuns.FirstOrDefault(s => s.StepId == stepId && s.Status == status);
            if (stepRun is not null)
            {
                return stepRun;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Step {stepId} in run {run.Id} did not reach {status} within {timeout}.");
    }

    private async Task<WorkflowInstance> WaitForWorkflowStatusAsync(AutomationRun run, WorkflowStatus status, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var refreshed = await _runRepository.GetAsync(run.Id);
            var workflowInstanceId = refreshed?.WorkflowInstanceId;
            if (!string.IsNullOrEmpty(workflowInstanceId))
            {
                var instance = await _persistence.GetWorkflowInstance(workflowInstanceId);
                if (instance is not null && instance.Status == status)
                {
                    return instance;
                }
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Workflow for run {run.Id} did not reach {status} within {timeout}.");
    }

    public async Task DisposeAsync()
    {
        await _workflowHost.StopAsync(CancellationToken.None);
        await _provider.DisposeAsync();
        _fixture.Dispose();
    }
}
