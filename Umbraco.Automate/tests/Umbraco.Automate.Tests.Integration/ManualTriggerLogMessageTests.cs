using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Actions.Middleware;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Conditions;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.ControlFlow;
using Umbraco.Automate.Core.Diagnostics;
using Umbraco.Automate.Core.Dispatch;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Bindings;
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
/// End-to-end smoke test: fires a Manual Trigger that executes a Log Message action.
/// Verifies the full execution pipeline without the outbox dispatcher loop.
/// </summary>
public class ManualTriggerLogMessageTests : IAsyncLifetime
{
    private ServiceProvider _provider = null!;
    private IWorkflowHost _workflowHost = null!;
    private EfCoreTestFixture _fixture = null!;
    private TriggerEventHandler _handler = null!;
    private IAutomationRunRepository _runRepository = null!;
    private Automation _automation = null!;
    private Workspace _workspace = null!;

    public async Task InitializeAsync()
    {
        _fixture = new EfCoreTestFixture();
        var dbContextFactory = new TestDbContextFactory(_fixture.CreateContext);
        var configuration = new ConfigurationBuilder().Build();

        // Collections — manually constructed without Umbraco TypeLoader.
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

        var middlewareCollection = new ActionMiddlewareCollection(Array.Empty<IActionMiddleware>);

        // Build the service provider with WorkflowCore and our services.
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddDebug());
        services.AddWorkflow();

        // Repositories (real EF Core with in-memory SQLite).
        _runRepository = new EFCoreAutomationRunRepository(dbContextFactory);
        services.AddSingleton(_runRepository);

        // Collections.
        services.AddSingleton(actions);
        services.AddSingleton(triggers);
        services.AddSingleton(middlewareCollection);
        var controlFlow = new ControlFlowCollection(Enumerable.Empty<IControlFlow>);
        services.AddSingleton(controlFlow);
        services.AddSingleton(new BindingEvaluator(Array.Empty<IBindingFilter>()));
        services.AddSingleton<SettingsBindingResolver>();
        services.AddSingleton<ConditionEvaluator>();
        services.AddSingleton<ActionMiddlewarePipeline>();
        services.AddMetrics();
        services.AddSingleton<AutomateMetrics>();

        // Workspace service — returns a known workspace so we can verify execution context.
        _workspace = new WorkspaceBuilder().WithName("Smoke Test Workspace").Build();
        var workspaceService = new Mock<IWorkspaceService>();
        workspaceService.Setup(w => w.GetWorkspaceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_workspace);
        services.AddSingleton(workspaceService.Object);

        services.AddSingleton(Mock.Of<IConnectionService>());

        // Rate limiting — disabled for tests.
        services.Configure<RateLimitingOptions>(o => o.Enabled = false);
        services.AddSingleton<IRateLimitService, RateLimitService>();

        // Execution.
        services.AddSingleton<IStepErrorClassifier, DefaultStepErrorClassifier>();
        services.AddSingleton<IWorkflowCompiler, WorkflowCompiler>();
        services.AddSingleton<IAutomationExecutor, AutomationExecutor>();

        _provider = services.BuildServiceProvider();

        // Start the WorkflowCore host so it processes queued workflows.
        _workflowHost = _provider.GetRequiredService<IWorkflowHost>();
        await _workflowHost.StartAsync(CancellationToken.None);

        // Build the test automation: Manual Trigger → Log Message.
        _automation = new AutomationBuilder()
            .WithAlias("test-manual-log")
            .WithName("Test Manual Log")
            .WithManualTrigger()
            .AddLogMessageStep("Hello from smoke test!")
            .Build();

        // Wire up the TriggerEventHandler with mocked service layer.
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
            _provider.GetRequiredService<ILogger<TriggerEventHandler>>());
    }

    [Fact]
    public async Task ManualTrigger_LogMessage_CreatesRunAndCompletesStep()
    {
        // Arrange — build the trigger event message as the outbox dispatcher would deliver it.
        var triggerMessage = new TriggerEventMessage
        {
            TriggerAlias = "umbracoAutomate.manual",
            InitiatorType = "system",
        };

        var body = JsonSerializer.Serialize(triggerMessage, JsonOptions.Default);

        // Act — invoke the handler directly (bypassing outbox dispatch loop).
        await _handler.HandleAsync(body, CancellationToken.None);

        // Assert — poll for the step run to complete.
        // WorkflowCore processes steps asynchronously in its background thread.
        var runs = await WaitForRunAsync(_automation.Id, timeout: TimeSpan.FromSeconds(10));

        runs.Items.ShouldNotBeEmpty();

        var run = runs.Items.First();
        run.AutomationId.ShouldBe(_automation.Id);
        run.InitiatedBy.ShouldBe("system");

        // Wait for the step run to be recorded.
        var completedRun = await WaitForStepRunAsync(run.Id, timeout: TimeSpan.FromSeconds(10));

        completedRun.ShouldNotBeNull();
        completedRun.StepRuns.ShouldNotBeEmpty();

        var stepRun = completedRun.StepRuns.First();
        stepRun.ActionAlias.ShouldBe("umbracoAutomate.logMessage");
        stepRun.Status.ShouldBe(StepRunStatus.Completed);
    }

    private async Task<(IEnumerable<AutomationRun> Items, int Total)> WaitForRunAsync(
        Guid automationId, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var result = await _runRepository.GetPagedByAutomationAsync(automationId);
            if (result.Items.Any())
            {
                return result;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"No automation run found within {timeout}.");
    }

    private async Task<AutomationRun?> WaitForStepRunAsync(Guid runId, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var run = await _runRepository.GetAsync(runId);
            if (run?.StepRuns.Count > 0 && run.StepRuns.All(s => s.Status != StepRunStatus.Running))
            {
                return run;
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
