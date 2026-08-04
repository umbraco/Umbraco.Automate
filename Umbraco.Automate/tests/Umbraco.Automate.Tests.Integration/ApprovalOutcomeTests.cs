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
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace Umbraco.Automate.Tests.Integration;

/// <summary>
/// End-to-end checks for the Request Approval step's named outcomes. A decision completes the step
/// either way and returns <c>approved</c> or <c>rejected</c>, so an author can wire the two canvas
/// handles to different steps without an intervening If.
/// </summary>
/// <remarks>
/// The compatibility test here is the important one. Automations built before the handles existed
/// have a single unlabelled edge out of their approval step, which
/// <c>WorkflowCompiler.WireTransitions</c> wires as a <see cref="ValueOutcome"/> with a null value.
/// Whether such an edge is still taken when the step returns a *named* outcome is WorkflowCore's
/// behaviour, not ours — so it is asserted rather than assumed.
/// </remarks>
public class ApprovalOutcomeTests : IAsyncLifetime
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
                new RequestApprovalAction(deps),
            };
        });

        var triggers = new TriggerCollection(() =>
        {
            var deps = new TriggerInfrastructure(modelResolver);
            return new ITrigger[] { new ManualTrigger(deps) };
        });

        var controlFlow = new ControlFlowCollection(Array.Empty<IControlFlow>);
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

        var workspace = new WorkspaceBuilder().WithName("Approval Outcome Workspace").Build();
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

        var nodeEligibility = new Mock<IExecutionNodeEligibility>();
        nodeEligibility.Setup(e => e.CanExecuteWorkflows()).Returns(true);

        _handler = new TriggerEventHandler(
            _automationServiceMock.Object,
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

    [Theory]
    [InlineData(ApprovalOutcome.Approved, "took-approved-path", StepRunStatus.Completed)]
    [InlineData(ApprovalOutcome.Rejected, "took-rejected-path", StepRunStatus.Rejected)]
    public async Task LabelledEdges_RouteEachDecisionToItsOwnStep(
        ApprovalOutcome outcome, string expectedMessage, StepRunStatus expectedApprovalStatus)
    {
        var approvalStep = ApprovalStep();
        var approvedStep = LogStep("approvedLog", "took-approved-path");
        var rejectedStep = LogStep("rejectedLog", "took-rejected-path");

        var automation = new AutomationBuilder()
            .WithAlias($"test-approval-branch-{outcome}")
            .WithName($"test-approval-branch-{outcome}")
            .WithManualTrigger()
            .AddStep(approvalStep)
            .AddStep(approvedStep)
            .AddStep(rejectedStep)
            .WithTriggerConnection(approvalStep.Id)
            .WithConnection(approvalStep.Id, approvedStep.Id, RequestApprovalAction.ApprovedOutcome)
            .WithConnection(approvalStep.Id, rejectedStep.Id, RequestApprovalAction.RejectedOutcome)
            .Build();

        var run = await RunToApprovalAsync(automation, approvalStep);
        await SubmitDecisionAsync(run.Id, approvalStep.Id, outcome);
        await WaitForWorkflowStatusAsync(run, WorkflowStatus.Complete, TimeSpan.FromSeconds(15));

        var completed = await _runRepository.GetAsync(run.Id);

        // The step is terminal either way, and never Failed: a refusal is Rejected.
        completed!.StepRuns.Single(s => s.StepId == approvalStep.Id).Status.ShouldBe(expectedApprovalStatus);

        // Only the matching branch ran.
        var messages = completed.StepRuns
            .Where(s => s.StepId != approvalStep.Id && s.OutputData is not null)
            .Select(s => ReadMessage(s.OutputData!))
            .ToList();
        messages.ShouldBe([expectedMessage]);
    }

    /// <summary>
    /// The compatibility guard. An automation built before the handles existed has one unlabelled
    /// edge out of its approval step; a named outcome must still traverse it, for both decisions.
    /// If this fails, returning a named outcome unconditionally is not safe and the step needs to
    /// know whether its outgoing edges are labelled.
    /// </summary>
    [Theory]
    [InlineData(ApprovalOutcome.Approved)]
    [InlineData(ApprovalOutcome.Rejected)]
    public async Task UnlabelledEdge_StillContinues_AfterNamedOutcome(ApprovalOutcome outcome)
    {
        var approvalStep = ApprovalStep();
        var nextStep = LogStep("afterApproval", "continued");

        var automation = new AutomationBuilder()
            .WithAlias($"test-approval-legacy-edge-{outcome}")
            .WithName($"test-approval-legacy-edge-{outcome}")
            .WithManualTrigger()
            .AddStep(approvalStep)
            .AddStep(nextStep)
            .WithTriggerConnection(approvalStep.Id)
            .WithConnection(approvalStep.Id, nextStep.Id)
            .Build();

        var run = await RunToApprovalAsync(automation, approvalStep);
        await SubmitDecisionAsync(run.Id, approvalStep.Id, outcome);

        var nextRun = await WaitForStepRunStatusAsync(run, nextStep.Id, StepRunStatus.Completed, TimeSpan.FromSeconds(15));
        ReadMessage(nextRun.OutputData!).ShouldBe("continued");
    }

    [Fact]
    public async Task Decision_IsStoredAsStepOutput_ForBranchingAndDisplay()
    {
        var approvalStep = ApprovalStep();
        var nextStep = LogStep("afterApproval", "continued");

        var automation = new AutomationBuilder()
            .WithAlias("test-approval-output")
            .WithName("test-approval-output")
            .WithManualTrigger()
            .AddStep(approvalStep)
            .AddStep(nextStep)
            .WithTriggerConnection(approvalStep.Id)
            .WithConnection(approvalStep.Id, nextStep.Id)
            .Build();

        var run = await RunToApprovalAsync(automation, approvalStep);
        await SubmitDecisionAsync(run.Id, approvalStep.Id, ApprovalOutcome.Rejected, "not this time");
        await WaitForStepRunStatusAsync(run, approvalStep.Id, StepRunStatus.Rejected, TimeSpan.FromSeconds(15));

        var completed = await _runRepository.GetAsync(run.Id);
        var approvalRun = completed!.StepRuns.Single(s => s.StepId == approvalStep.Id);

        using var doc = JsonDocument.Parse(approvalRun.OutputData!);
        doc.RootElement.GetProperty("approved").GetBoolean().ShouldBeFalse();
        doc.RootElement.GetProperty("outcome").GetString().ShouldBe("Rejected");
        doc.RootElement.GetProperty("comment").GetString().ShouldBe("not this time");
    }

    private static StepConfiguration ApprovalStep() => new()
    {
        Id = Guid.NewGuid(),
        ActionAlias = RequestApprovalAction.ApprovalActionAlias,
        Name = "Approval",
        Alias = "approval",
        Settings = new Dictionary<string, object?> { ["prompt"] = "Please approve" },
    };

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

    private async Task<AutomationRun> RunToApprovalAsync(Automation automation, StepConfiguration approvalStep)
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

        var run = await WaitForRunAsync(automation.Id, TimeSpan.FromSeconds(15));
        await WaitForStepRunStatusAsync(run, approvalStep.Id, StepRunStatus.WaitingForInput, TimeSpan.FromSeconds(15));
        return run;
    }

    /// <summary>Publishes the approval event exactly as <c>SubmitApprovalController</c> does.</summary>
    private Task SubmitDecisionAsync(Guid runId, Guid stepId, ApprovalOutcome outcome, string? comment = null)
        => _workflowHost.PublishEvent(
            RequestApprovalAction.ApprovalEventName,
            $"{runId}:{stepId}",
            new ApprovalDecision
            {
                Outcome = outcome,
                Comment = comment,
                ApprovedByUserKey = Guid.NewGuid(),
                DecisionUtc = DateTime.UtcNow,
            });

    private static string ReadMessage(string outputData)
    {
        using var doc = JsonDocument.Parse(outputData);
        return doc.RootElement.GetProperty("message").GetString()!;
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
