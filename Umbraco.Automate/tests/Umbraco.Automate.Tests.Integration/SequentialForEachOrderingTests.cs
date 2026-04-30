using System.Text.Json;
using Microsoft.Extensions.Configuration;
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
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Messaging;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Automate.Core.Versioning;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Persistence.Runs;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Automate.Tests.Common.Fixtures;
using WorkflowCore.Interface;

namespace Umbraco.Automate.Tests.Integration;

/// <summary>
/// End-to-end ordering check for sequential ForEach. Drives a real WorkflowCore engine
/// with a body containing two LogMessage steps (A → B) over a 3-item collection, then
/// asserts the order in which the body steps complete.
///
/// Sequential ForEach is supposed to run the body end-to-end per iteration:
///   A0 → B0 → A1 → B1 → A2 → B2  (DFS — correct)
///
/// If iterations interleave step-by-step instead, we'd see:
///   A0 → A1 → A2 → B0 → B1 → B2  (BFS — would mean step.* bindings inside the body
///   read the wrong iteration's output, because data.StepOutputs is keyed only by
///   step id and gets overwritten on every action invocation.)
/// </summary>
public class SequentialForEachOrderingTests : IAsyncLifetime
{
    private ServiceProvider _provider = null!;
    private IWorkflowHost _workflowHost = null!;
    private EfCoreTestFixture _fixture = null!;
    private TriggerEventHandler _handler = null!;
    private IAutomationRunRepository _runRepository = null!;
    private Automation _automation = null!;
    private Workspace _workspace = null!;
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
        services.AddSingleton(new BindingEvaluator(Array.Empty<IBindingFilter>()));
        services.AddSingleton<SettingsBindingResolver>();
        services.AddSingleton<ConditionEvaluator>();
        services.AddSingleton<ActionMiddlewarePipeline>();
        services.AddMetrics();
        services.AddSingleton<AutomateMetrics>();

        _workspace = new WorkspaceBuilder().WithName("ForEach Ordering Workspace").Build();
        var workspaceService = new Mock<IWorkspaceService>();
        workspaceService.Setup(w => w.GetWorkspaceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_workspace);
        services.AddSingleton(workspaceService.Object);

        services.AddSingleton(Mock.Of<IConnectionService>());

        services.Configure<RateLimitingOptions>(o => o.Enabled = false);
        services.AddSingleton<IRateLimitService, RateLimitService>();

        services.AddSingleton<IStepErrorClassifier, DefaultStepErrorClassifier>();
        services.AddSingleton<IWorkflowCompiler, WorkflowCompiler>();
        services.AddSingleton<IAutomationExecutor, AutomationExecutor>();

        _provider = services.BuildServiceProvider();

        _workflowHost = _provider.GetRequiredService<IWorkflowHost>();
        await _workflowHost.StartAsync(CancellationToken.None);

        // Build: ManualTrigger → ForEach(a,b,c, sequential) → LogMessage A → LogMessage B
        // Each LogMessage encodes the iteration index in its message via the loop binding,
        // so we can recover (iteration, step) from each StepRun's OutputData.
        var forEachStep = new StepConfiguration
        {
            Id = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.forEach",
            Name = "ForEach",
            Alias = "forEach",
            Settings = new Dictionary<string, object?>
            {
                ["collection"] = "a,b,c",
                ["runParallel"] = false,
            },
        };
        var logA = new StepConfiguration
        {
            Id = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.logMessage",
            Name = "Log A",
            Alias = "logA",
            Settings = new Dictionary<string, object?>
            {
                ["message"] = "${ loop.index }-A",
                ["logLevel"] = "Information",
            },
        };
        var logB = new StepConfiguration
        {
            Id = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.logMessage",
            Name = "Log B",
            Alias = "logB",
            Settings = new Dictionary<string, object?>
            {
                ["message"] = "${ loop.index }-B",
                ["logLevel"] = "Information",
            },
        };

        _automation = new AutomationBuilder()
            .WithAlias("test-foreach-ordering")
            .WithName("Test ForEach Ordering")
            .WithManualTrigger()
            .AddStep(forEachStep)
            .AddStep(logA)
            .AddStep(logB)
            .WithTriggerConnection(forEachStep.Id)
            .WithConnection(forEachStep.Id, logA.Id)
            .WithConnection(logA.Id, logB.Id)
            .Build();

        _automationServiceMock = new Mock<IAutomationService>();
        _automationServiceMock.Setup(s => s.GetAllAutomationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { _automation });

        var versionService = new Mock<IEntityVersionService>();

        var nodeEligibility = new Mock<IExecutionNodeEligibility>();
        nodeEligibility.Setup(e => e.CanExecuteWorkflows()).Returns(true);

        _handler = new TriggerEventHandler(
            _automationServiceMock.Object,
            versionService.Object,
            _provider.GetRequiredService<IAutomationExecutor>(),
            nodeEligibility.Object,
            triggers,
            _provider.GetRequiredService<ILogger<TriggerEventHandler>>());
    }

    [Fact]
    public async Task SequentialForEach_BodyStepBindings_ResolvePerIteration()
    {
        // Mirrors the user's reported automation shape: sequential ForEach with a body
        // chain whose later step reads an earlier step's output via ${ steps.<alias> }.
        // Without per-iteration scoping, a later iteration's first step would clobber
        // the global StepOutputs entry before earlier iterations' downstream steps read
        // it — exactly the failure mode that caused both publishContent invocations to
        // target the same document in the partner-requirements-check run.
        var forEachStep = new StepConfiguration
        {
            Id = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.forEach",
            Name = "ForEach",
            Alias = "forEachSeqBindings",
            Settings = new Dictionary<string, object?>
            {
                ["collection"] = "alpha,beta,gamma",
                ["runParallel"] = false,
            },
        };
        var logA = new StepConfiguration
        {
            Id = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.logMessage",
            Name = "Sequential A",
            Alias = "sequentialA",
            Settings = new Dictionary<string, object?>
            {
                ["message"] = "iter-${ loop.index }-item-${ loop.item }",
                ["logLevel"] = "Information",
            },
        };
        var logB = new StepConfiguration
        {
            Id = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.logMessage",
            Name = "Sequential B (reads A)",
            Alias = "sequentialB",
            Settings = new Dictionary<string, object?>
            {
                ["message"] = "B-saw:${ steps.sequentialA.message }",
                ["logLevel"] = "Information",
            },
        };

        var sequentialBindingsAutomation = new AutomationBuilder()
            .WithAlias("test-foreach-sequential-iteration-scoping")
            .WithName("Test ForEach Sequential Iteration Scoping")
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
            .ReturnsAsync(new[] { _automation, sequentialBindingsAutomation });

        var triggerMessage = new TriggerEventMessage
        {
            TriggerAlias = "umbracoAutomate.manual",
            InitiatorType = "system",
        };
        var body = JsonSerializer.Serialize(triggerMessage, JsonOptions.Default);
        await _handler.HandleAsync(body, CancellationToken.None);

        var run = await WaitForRunAsync(sequentialBindingsAutomation.Id, TimeSpan.FromSeconds(15));
        var completed = await WaitForBodyStepRunsAsync(run.Id, expectedAtLeast: 6, TimeSpan.FromSeconds(15));

        // Order by StartedUtc so we get the natural execution sequence: A0, B0, A1, B1, A2, B2.
        var bodyMessages = completed.StepRuns
            .Where(s => s.ActionAlias == "umbracoAutomate.logMessage")
            .OrderBy(s => s.StartedUtc)
            .Select(ExtractMessage)
            .ToList();

        bodyMessages.Count.ShouldBe(6);
        bodyMessages.ShouldBe(new[]
        {
            "iter-0-item-alpha",
            "B-saw:iter-0-item-alpha",
            "iter-1-item-beta",
            "B-saw:iter-1-item-beta",
            "iter-2-item-gamma",
            "B-saw:iter-2-item-gamma",
        });
    }

    [Fact]
    public async Task ParallelForEach_BodyStepBindings_ResolvePerIteration()
    {
        // Build a separate automation: ForEach(parallel) with body LogA → LogB,
        // where LogB's message reads ${ steps.logA.message }. Without iteration-scoped
        // step outputs, LogB would resolve to whichever LogA wrote to data.StepOutputs
        // last, not its own iteration's value — and we'd see duplicates / cross-talk.
        var forEachStep = new StepConfiguration
        {
            Id = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.forEach",
            Name = "ForEach",
            Alias = "forEachParallel",
            Settings = new Dictionary<string, object?>
            {
                ["collection"] = "x,y,z",
                ["runParallel"] = true,
            },
        };
        var logA = new StepConfiguration
        {
            Id = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.logMessage",
            Name = "Parallel A",
            Alias = "parallelA",
            Settings = new Dictionary<string, object?>
            {
                ["message"] = "iter-${ loop.index }-item-${ loop.item }",
                ["logLevel"] = "Information",
            },
        };
        var logB = new StepConfiguration
        {
            Id = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.logMessage",
            Name = "Parallel B (reads A)",
            Alias = "parallelB",
            Settings = new Dictionary<string, object?>
            {
                ["message"] = "B-saw:${ steps.parallelA.message }",
                ["logLevel"] = "Information",
            },
        };

        var parallelAutomation = new AutomationBuilder()
            .WithAlias("test-foreach-parallel-iteration-scoping")
            .WithName("Test ForEach Parallel Iteration Scoping")
            .WithManualTrigger()
            .AddStep(forEachStep)
            .AddStep(logA)
            .AddStep(logB)
            .WithTriggerConnection(forEachStep.Id)
            .WithConnection(forEachStep.Id, logA.Id)
            .WithConnection(logA.Id, logB.Id)
            .Build();

        // Re-register the parallel automation alongside the sequential one so the
        // manual trigger fans out to both. We then wait on the parallel automation's run.
        _automationServiceMock
            .Setup(s => s.GetAllAutomationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { _automation, parallelAutomation });

        var triggerMessage = new TriggerEventMessage
        {
            TriggerAlias = "umbracoAutomate.manual",
            InitiatorType = "system",
        };
        var body = JsonSerializer.Serialize(triggerMessage, JsonOptions.Default);
        await _handler.HandleAsync(body, CancellationToken.None);

        var run = await WaitForRunAsync(parallelAutomation.Id, TimeSpan.FromSeconds(15));
        var completed = await WaitForBodyStepRunsAsync(run.Id, expectedAtLeast: 6, TimeSpan.FromSeconds(15));

        var logBMessages = completed.StepRuns
            .Where(s => s.ActionAlias == "umbracoAutomate.logMessage" && (s.OutputData ?? string.Empty).Contains("B-saw:"))
            .Select(ExtractMessage)
            .OrderBy(m => m)
            .ToList();

        // Each iteration's logB should resolve steps.parallelA.message to its own iteration's
        // logA output, not whichever ran last. Three distinct, complete pairings.
        logBMessages.Count.ShouldBe(3);
        logBMessages.ShouldBe(new[]
        {
            "B-saw:iter-0-item-x",
            "B-saw:iter-1-item-y",
            "B-saw:iter-2-item-z",
        });
    }

    [Fact]
    public async Task SequentialForEach_RunsBodyEndToEndPerIteration()
    {
        var triggerMessage = new TriggerEventMessage
        {
            TriggerAlias = "umbracoAutomate.manual",
            InitiatorType = "system",
        };
        var body = JsonSerializer.Serialize(triggerMessage, JsonOptions.Default);

        await _handler.HandleAsync(body, CancellationToken.None);

        var run = await WaitForRunAsync(_automation.Id, TimeSpan.FromSeconds(15));
        var completed = await WaitForBodyStepRunsAsync(run.Id, expectedAtLeast: 6, TimeSpan.FromSeconds(15));

        // The body has two LogMessage steps; we expect 3 iterations × 2 = 6 body StepRuns,
        // plus one StepRun for the ForEach container itself. Filter to body steps only,
        // ordered by StartedUtc as the repository returns them.
        var bodyMessages = completed.StepRuns
            .Where(s => s.ActionAlias == "umbracoAutomate.logMessage")
            .OrderBy(s => s.StartedUtc)
            .Select(ExtractMessage)
            .ToList();

        bodyMessages.Count.ShouldBe(6);

        // DFS (correct):  0-A, 0-B, 1-A, 1-B, 2-A, 2-B
        // BFS (the bug):  0-A, 1-A, 2-A, 0-B, 1-B, 2-B
        bodyMessages.ShouldBe(new[] { "0-A", "0-B", "1-A", "1-B", "2-A", "2-B" });
    }

    private static string ExtractMessage(StepRun stepRun)
    {
        if (string.IsNullOrEmpty(stepRun.OutputData))
        {
            return string.Empty;
        }

        using var doc = JsonDocument.Parse(stepRun.OutputData);
        return doc.RootElement.TryGetProperty("message", out var msg) ? msg.GetString() ?? string.Empty : string.Empty;
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

    private async Task<AutomationRun> WaitForBodyStepRunsAsync(Guid runId, int expectedAtLeast, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var run = await _runRepository.GetAsync(runId);
            if (run is not null)
            {
                var bodyCount = run.StepRuns.Count(s => s.ActionAlias == "umbracoAutomate.logMessage");
                var allTerminal = run.StepRuns.All(s => s.Status != StepRunStatus.Running);
                if (bodyCount >= expectedAtLeast && allTerminal)
                {
                    return run;
                }
            }

            await Task.Delay(100);
        }

        throw new TimeoutException(
            $"Expected at least {expectedAtLeast} logMessage step runs for run {runId} within {timeout}.");
    }

    public async Task DisposeAsync()
    {
        await _workflowHost.StopAsync(CancellationToken.None);
        await _provider.DisposeAsync();
        _fixture.Dispose();
    }
}
