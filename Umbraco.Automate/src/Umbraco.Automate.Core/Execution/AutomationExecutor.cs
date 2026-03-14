using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.Middleware;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.Diagnostics;
using Umbraco.Automate.Core.Bindings;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Workspaces;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// Default <see cref="IAutomationExecutor"/> that compiles an automation definition
/// into a WorkflowCore workflow and starts execution.
/// </summary>
internal sealed class AutomationExecutor : IAutomationExecutor
{
    private readonly IWorkflowHost _workflowHost;
    private readonly IWorkflowRegistry _workflowRegistry;
    private readonly ActionCollection _actions;
    private readonly ActionMiddlewarePipeline _pipeline;
    private readonly BindingEvaluator _bindingEvaluator;
    private readonly SettingsBindingResolver _settingsBindingResolver;
    private readonly IAutomationRunRepository _runRepository;
    private readonly IConnectionService _connectionService;
    private readonly IWorkspaceService _workspaceService;
    private readonly IRateLimitService _rateLimitService;
    private readonly AutomateMetrics _metrics;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AutomationExecutor> _logger;

    public AutomationExecutor(
        IWorkflowHost workflowHost,
        IWorkflowRegistry workflowRegistry,
        ActionCollection actions,
        ActionMiddlewarePipeline pipeline,
        BindingEvaluator bindingEvaluator,
        SettingsBindingResolver settingsBindingResolver,
        IAutomationRunRepository runRepository,
        IConnectionService connectionService,
        IWorkspaceService workspaceService,
        IRateLimitService rateLimitService,
        AutomateMetrics metrics,
        IServiceProvider serviceProvider,
        ILogger<AutomationExecutor> logger)
    {
        _workflowHost = workflowHost;
        _workflowRegistry = workflowRegistry;
        _actions = actions;
        _pipeline = pipeline;
        _bindingEvaluator = bindingEvaluator;
        _settingsBindingResolver = settingsBindingResolver;
        _runRepository = runRepository;
        _connectionService = connectionService;
        _workspaceService = workspaceService;
        _rateLimitService = rateLimitService;
        _metrics = metrics;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<Guid> ExecuteAsync(
        Automation automation,
        string initiatorType,
        string? initiatorId,
        Dictionary<string, object?>? triggerOutputData,
        CancellationToken cancellationToken)
    {
        // Check rate limits before creating the run record.
        await _rateLimitService.CheckRateLimitAsync(automation.Id, cancellationToken);

        // Resolve workspace and service account.
        var workspace = await _workspaceService.GetWorkspaceAsync(automation.WorkspaceId, cancellationToken)
            ?? throw new InvalidOperationException($"Workspace '{automation.WorkspaceId}' not found for automation '{automation.Name}'.");

        // Create the automation run record.
        var run = new AutomationRun
        {
            Id = Guid.NewGuid(),
            AutomationId = automation.Id,
            AutomationVersion = automation.PublishedVersion ?? automation.Version,
            WorkspaceId = workspace.Id,
            ServiceAccountKey = workspace.ServiceAccountKey,
            Status = AutomationRunStatus.Running,
            StartedUtc = DateTime.UtcNow,
            InitiatedBy = initiatorType,
            CorrelationId = initiatorId,
            TriggerData = triggerOutputData is not null
                ? JsonSerializer.Serialize(triggerOutputData, Dispatch.JsonOptions.Default)
                : null,
        };

        await _runRepository.SaveAsync(run, cancellationToken);

        _metrics.RunStarted(automation.Alias);

        // Set the execution context for the current async flow.
        var executionContext = new AutomationExecutionContext
        {
            ServiceAccountKey = workspace.ServiceAccountKey,
            WorkspaceId = workspace.Id,
            WorkspaceName = workspace.Name,
            AutomationId = automation.Id,
            AutomationName = automation.Name,
            RunId = run.Id,
            InitiatorType = initiatorType,
            InitiatorId = initiatorId,
        };

        using var _ = ExecutionContextAccessor.Set(executionContext);

        // Register the workflow definition if not already registered.
        var workflowId = $"automate-{automation.Id}-v{automation.Version}";
        var existing = _workflowRegistry.GetDefinition(workflowId);

        if (existing is null)
        {
            var definition = CompileWorkflowDefinition(automation, workflowId);
            _workflowRegistry.RegisterWorkflow(definition);
        }

        // Start the workflow with our data.
        var workflowData = new AutomationWorkflowData
        {
            RunId = run.Id,
            AutomationId = automation.Id,
            AutomationAlias = automation.Alias,
            TriggerOutput = triggerOutputData ?? [],
            ExecutionContext = executionContext,
        };

        var instanceId = await _workflowHost.StartWorkflow(workflowId, workflowData);

        _logger.LogInformation(
            "Started workflow {WorkflowId} (instance {InstanceId}) for run {RunId} as service account {ServiceAccountKey}",
            workflowId, instanceId, run.Id, workspace.ServiceAccountKey);

        return run.Id;
    }

    private WorkflowDefinition CompileWorkflowDefinition(Automation automation, string workflowId)
    {
        var definition = new WorkflowDefinition
        {
            Id = workflowId,
            Version = automation.Version,
            DataType = typeof(AutomationWorkflowData),
        };

        // Sort steps by connections to determine execution order.
        var orderedSteps = TopologicalSort(automation.Steps, automation.Connections);
        var stepIndex = 0;
        var stepIdToIndex = new Dictionary<Guid, int>();

        foreach (var stepConfig in orderedSteps)
        {
            var action = _actions.FirstOrDefault(a => a.Alias == stepConfig.ActionAlias);
            if (action is null)
            {
                _logger.LogWarning("Action '{ActionAlias}' not found, skipping step {StepId}", stepConfig.ActionAlias, stepConfig.Id);
                continue;
            }

            var stepBody = new ActionStepBody(
                stepConfig,
                action,
                _pipeline,
                _bindingEvaluator,
                _settingsBindingResolver,
                _runRepository,
                _connectionService,
                _metrics,
                _serviceProvider.GetRequiredService<ILogger<ActionStepBody>>());

            var currentIndex = stepIndex++;
            stepIdToIndex[stepConfig.Id] = currentIndex;

            var workflowStep = new ActionWorkflowStep(stepBody)
            {
                Id = currentIndex,
                Name = stepConfig.Name,
            };

            definition.Steps.Add(workflowStep);
        }

        // Wire up step transitions.
        if (automation.Connections.Count > 0)
        {
            // Connection-aware wiring: use outcome values from connections.
            foreach (var connection in automation.Connections)
            {
                if (!stepIdToIndex.TryGetValue(connection.SourceStepId, out var sourceIndex) ||
                    !stepIdToIndex.TryGetValue(connection.TargetStepId, out var targetIndex))
                {
                    continue;
                }

                var outcome = new ValueOutcome { NextStep = targetIndex };
                if (connection.Outcome is not null)
                {
                    // WorkflowCore matches: ValueOutcome.GetValue(data) == ExecutionResult.OutcomeValue
                    // When Value is null (no lambda), the outcome matches any result (default/sequential).
                    var outcomeValue = connection.Outcome;
                    Expression<Func<AutomationWorkflowData, object>> expr = _ => outcomeValue;
                    outcome.Value = expr;
                }

                definition.Steps.FindById(sourceIndex).Outcomes.Add(outcome);
            }
        }
        else
        {
            // Sequential fallback: each step goes to the next.
            for (var i = 0; i < definition.Steps.Count - 1; i++)
            {
                definition.Steps.FindById(i).Outcomes.Add(new ValueOutcome { NextStep = i + 1 });
            }
        }

        return definition;
    }

    /// <summary>
    /// Orders steps by their connections using topological sort.
    /// Falls back to original order if no connections exist.
    /// </summary>
    private static List<StepConfiguration> TopologicalSort(
        IList<StepConfiguration> steps,
        IList<StepConnection> connections)
    {
        if (connections.Count == 0)
        {
            return [..steps];
        }

        var adjacency = new Dictionary<Guid, List<Guid>>();
        var inDegree = new Dictionary<Guid, int>();

        foreach (var step in steps)
        {
            adjacency[step.Id] = [];
            inDegree[step.Id] = 0;
        }

        foreach (var conn in connections)
        {
            if (adjacency.ContainsKey(conn.SourceStepId) && inDegree.ContainsKey(conn.TargetStepId))
            {
                adjacency[conn.SourceStepId].Add(conn.TargetStepId);
                inDegree[conn.TargetStepId]++;
            }
        }

        var queue = new Queue<Guid>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var result = new List<StepConfiguration>();
        var stepLookup = steps.ToDictionary(s => s.Id);

        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (stepLookup.TryGetValue(id, out var step))
            {
                result.Add(step);
            }

            foreach (var neighbor in adjacency[id])
            {
                inDegree[neighbor]--;
                if (inDegree[neighbor] == 0)
                {
                    queue.Enqueue(neighbor);
                }
            }
        }

        // If topological sort didn't include all steps (cycle), fall back.
        if (result.Count < steps.Count)
        {
            return [..steps];
        }

        return result;
    }
}
