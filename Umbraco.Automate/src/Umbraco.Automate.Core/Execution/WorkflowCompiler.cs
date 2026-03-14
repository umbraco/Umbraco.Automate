using System.Linq.Expressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.Middleware;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Bindings;
using Umbraco.Automate.Core.Conditions;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.ControlFlow;
using Umbraco.Automate.Core.ControlFlow.BuiltIn;
using Umbraco.Automate.Core.Diagnostics;
using Umbraco.Automate.Core.Execution.ControlFlow;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.StepTypes;
using WorkflowCore.Models;

namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// Default <see cref="IWorkflowCompiler"/> that compiles an automation definition
/// into a WorkflowCore workflow definition by resolving step aliases against both
/// the action and control flow collections.
/// </summary>
internal sealed class WorkflowCompiler : IWorkflowCompiler
{
    private readonly ActionCollection _actions;
    private readonly ControlFlowCollection _controlFlow;
    private readonly ActionMiddlewarePipeline _pipeline;
    private readonly BindingEvaluator _bindingEvaluator;
    private readonly SettingsBindingResolver _settingsBindingResolver;
    private readonly ConditionEvaluator _conditionEvaluator;
    private readonly IAutomationRunRepository _runRepository;
    private readonly IConnectionService _connectionService;
    private readonly AutomateMetrics _metrics;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WorkflowCompiler> _logger;

    public WorkflowCompiler(
        ActionCollection actions,
        ControlFlowCollection controlFlow,
        ActionMiddlewarePipeline pipeline,
        BindingEvaluator bindingEvaluator,
        SettingsBindingResolver settingsBindingResolver,
        ConditionEvaluator conditionEvaluator,
        IAutomationRunRepository runRepository,
        IConnectionService connectionService,
        AutomateMetrics metrics,
        IServiceProvider serviceProvider,
        ILogger<WorkflowCompiler> logger)
    {
        _actions = actions;
        _controlFlow = controlFlow;
        _pipeline = pipeline;
        _bindingEvaluator = bindingEvaluator;
        _settingsBindingResolver = settingsBindingResolver;
        _conditionEvaluator = conditionEvaluator;
        _runRepository = runRepository;
        _connectionService = connectionService;
        _metrics = metrics;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public WorkflowDefinition Compile(Automation automation, string workflowId)
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
            var workflowStep = CompileStep(stepConfig);
            if (workflowStep is null)
            {
                continue;
            }

            var currentIndex = stepIndex++;
            stepIdToIndex[stepConfig.Id] = currentIndex;

            workflowStep.Id = currentIndex;
            workflowStep.Name = stepConfig.Name;

            definition.Steps.Add(workflowStep);
        }

        // Wire up step transitions.
        WireTransitions(definition, automation.Connections, stepIdToIndex);

        return definition;
    }

    private WorkflowStep? CompileStep(StepConfiguration stepConfig)
    {
        // Try action collection first.
        var action = _actions.GetByAlias(stepConfig.ActionAlias);
        if (action is not null)
        {
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

            return new ActionWorkflowStep(stepBody);
        }

        // Try control flow collection.
        var controlFlow = _controlFlow.GetByAlias(stepConfig.ActionAlias);
        if (controlFlow is not null)
        {
            return CompileControlFlowStep(stepConfig, controlFlow);
        }

        _logger.LogWarning("Step type '{ActionAlias}' not found in action or control flow collections, skipping step {StepId}",
            stepConfig.ActionAlias, stepConfig.Id);
        return null;
    }

    private ControlFlowWorkflowStep? CompileControlFlowStep(StepConfiguration stepConfig, IControlFlow controlFlow)
    {
        switch (controlFlow)
        {
            case IfControlFlow:
            {
                var settings = ResolveSettings<IfControlFlowSettings>(stepConfig, controlFlow) ?? new IfControlFlowSettings();
                return new ControlFlowWorkflowStep(new IfStepBody(settings, _conditionEvaluator));
            }

            case SwitchControlFlow:
            {
                var settings = ResolveSettings<SwitchControlFlowSettings>(stepConfig, controlFlow) ?? new SwitchControlFlowSettings();
                return new ControlFlowWorkflowStep(new SwitchStepBody(settings, _conditionEvaluator));
            }

            // ForEach, While, Parallel — container compilation will be added in Phase 6-8.
            default:
                _logger.LogWarning("Control flow type '{Alias}' does not have a compiled step body yet, skipping step {StepId}",
                    controlFlow.Alias, stepConfig.Id);
                return null;
        }
    }

    private static TSettings? ResolveSettings<TSettings>(StepConfiguration stepConfig, IControlFlow controlFlow)
        where TSettings : class
    {
        if (stepConfig.Settings.Count == 0)
        {
            return null;
        }

        return controlFlow.ResolveSettings(stepConfig.Settings) as TSettings;
    }

    private static void WireTransitions(
        WorkflowDefinition definition,
        IList<StepConnection> connections,
        Dictionary<Guid, int> stepIdToIndex)
    {
        if (connections.Count > 0)
        {
            // Connection-aware wiring: use outcome values from connections.
            foreach (var connection in connections)
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
    }

    /// <summary>
    /// Orders steps by their connections using topological sort.
    /// Falls back to original order if no connections exist.
    /// </summary>
    internal static List<StepConfiguration> TopologicalSort(
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
