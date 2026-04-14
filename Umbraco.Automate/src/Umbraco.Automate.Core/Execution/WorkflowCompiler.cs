using System.Linq.Expressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Configuration;
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
    private readonly ControlFlowCollection _controlFlows;
    private readonly ActionMiddlewarePipeline _pipeline;
    private readonly BindingEvaluator _bindingEvaluator;
    private readonly SettingsBindingResolver _settingsBindingResolver;
    private readonly ConditionEvaluator _conditionEvaluator;
    private readonly IAutomationRunRepository _runRepository;
    private readonly IConnectionService _connectionService;
    private readonly IStepErrorClassifier _errorClassifier;
    private readonly AutomateMetrics _metrics;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WorkflowCompiler> _logger;

    public WorkflowCompiler(
        ActionCollection actions,
        ControlFlowCollection controlFlows,
        ActionMiddlewarePipeline pipeline,
        BindingEvaluator bindingEvaluator,
        SettingsBindingResolver settingsBindingResolver,
        ConditionEvaluator conditionEvaluator,
        IAutomationRunRepository runRepository,
        IConnectionService connectionService,
        IStepErrorClassifier errorClassifier,
        AutomateMetrics metrics,
        IServiceProvider serviceProvider,
        ILogger<WorkflowCompiler> logger)
    {
        _actions = actions;
        _controlFlows = controlFlows;
        _pipeline = pipeline;
        _bindingEvaluator = bindingEvaluator;
        _settingsBindingResolver = settingsBindingResolver;
        _conditionEvaluator = conditionEvaluator;
        _runRepository = runRepository;
        _connectionService = connectionService;
        _errorClassifier = errorClassifier;
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

        // Identify which steps are container control flow types (ForEach, While, Parallel).
        var containerStepIds = IdentifyContainerSteps(automation.Steps);

        // Run graph analysis to determine children and convergence for containers.
        var containerScopes = GraphAnalyzer.Analyze(automation.Connections, containerStepIds);

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

        // Wire up step transitions (outcomes).
        // Container steps are excluded — their child relationships are handled via Children + Branch().
        WireTransitions(definition, automation.Connections, stepIdToIndex, containerStepIds);

        // Wire up container children and convergence outcomes — must happen after all steps are indexed.
        WireContainerChildren(definition, containerScopes, stepIdToIndex);

        return definition;
    }

    private HashSet<Guid> IdentifyContainerSteps(IList<StepConfiguration> steps)
    {
        var containerIds = new HashSet<Guid>();

        foreach (var step in steps)
        {
            var controlFlow = _controlFlows.GetByAlias(step.ActionAlias);
            if (controlFlow is ForEachControlFlow or WhileControlFlow or ParallelControlFlow)
            {
                containerIds.Add(step.Id);
            }
        }

        return containerIds;
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
                _errorClassifier,
                _serviceProvider.GetRequiredService<IOptions<ExecutionOptions>>(),
                _metrics,
                _serviceProvider.GetRequiredService<ILogger<ActionStepBody>>());

            var workflowStep = new ActionWorkflowStep(stepBody);
            ApplyErrorBehavior(workflowStep, stepConfig, _serviceProvider.GetRequiredService<IOptions<ExecutionOptions>>().Value);
            return workflowStep;
        }

        // Try control flow collection.
        var controlFlow = _controlFlows.GetByAlias(stepConfig.ActionAlias);
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
                return new ControlFlowWorkflowStep(new IfStepBody(stepConfig, settings, _conditionEvaluator, _runRepository));
            }

            case SwitchControlFlow:
            {
                var settings = ResolveSettings<SwitchControlFlowSettings>(stepConfig, controlFlow) ?? new SwitchControlFlowSettings();
                return new ControlFlowWorkflowStep(new SwitchStepBody(stepConfig, settings, _conditionEvaluator, _runRepository));
            }

            case ForEachControlFlow:
            {
                var settings = ResolveSettings<ForEachControlFlowSettings>(stepConfig, controlFlow) ?? new ForEachControlFlowSettings();
                return new ControlFlowWorkflowStep(new ForEachContainerStepBody(stepConfig, settings, _bindingEvaluator, _runRepository));
            }

            case WhileControlFlow:
            {
                var settings = ResolveSettings<WhileControlFlowSettings>(stepConfig, controlFlow) ?? new WhileControlFlowSettings();
                return new ControlFlowWorkflowStep(new WhileContainerStepBody(stepConfig, settings, _conditionEvaluator, _runRepository));
            }

            case ParallelControlFlow:
            {
                return new ControlFlowWorkflowStep(new ParallelContainerStepBody());
            }

            default:
                _logger.LogWarning("Control flow type '{Alias}' does not have a compiled step body, skipping step {StepId}",
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

    /// <summary>
    /// Applies the step's configured error behavior to the WorkflowCore <see cref="WorkflowStep"/>.
    /// Without this, WorkflowCore falls back to the workflow's <c>DefaultErrorBehavior</c> (Retry)
    /// and retries every throwing step indefinitely — including ones where retry cannot help.
    /// Our <see cref="StepErrorBehavior"/> values intentionally mirror WorkflowCore's
    /// <see cref="WorkflowErrorHandling"/> so the cast is safe. The retry interval falls back
    /// to <see cref="ExecutionOptions.DefaultRetryInterval"/> when not set per-step, overriding
    /// WorkflowCore's built-in 60-second default.
    /// </summary>
    private static void ApplyErrorBehavior(WorkflowStep workflowStep, StepConfiguration stepConfig, ExecutionOptions executionOptions)
    {
        workflowStep.ErrorBehavior = (WorkflowErrorHandling)stepConfig.ErrorBehavior;
        workflowStep.RetryInterval = stepConfig.RetryInterval ?? executionOptions.DefaultRetryInterval;
    }

    private static void WireTransitions(
        WorkflowDefinition definition,
        IList<StepConnection> connections,
        Dictionary<Guid, int> stepIdToIndex,
        IReadOnlySet<Guid> containerStepIds)
    {
        if (connections.Count > 0)
        {
            // Connection-aware wiring: use outcome values from connections.
            foreach (var connection in connections)
            {
                // Skip connections originating from container steps — their transitions
                // are handled by WireContainerChildren via Children + Branch().
                if (containerStepIds.Contains(connection.SourceStepId))
                {
                    continue;
                }

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
    /// Populates <see cref="WorkflowStep.Children"/> for container control flow steps
    /// using the graph analysis results. This is required for WorkflowCore's
    /// <c>ExecutionResult.Branch()</c> to know which steps belong to the container body.
    /// </summary>
    private static void WireContainerChildren(
        WorkflowDefinition definition,
        Dictionary<Guid, ContainerScope> containerScopes,
        Dictionary<Guid, int> stepIdToIndex)
    {
        foreach (var (containerStepId, scope) in containerScopes)
        {
            if (!stepIdToIndex.TryGetValue(containerStepId, out var containerIndex))
            {
                continue;
            }

            var containerStep = definition.Steps.FindById(containerIndex);

            // Add child step indices.
            foreach (var childStepId in scope.ChildStepIds)
            {
                if (stepIdToIndex.TryGetValue(childStepId, out var childIndex))
                {
                    containerStep.Children.Add(childIndex);
                }
            }

            // Wire convergence: container's outcome points to the convergence step.
            if (scope.ConvergenceStepId.HasValue &&
                stepIdToIndex.TryGetValue(scope.ConvergenceStepId.Value, out var convergenceIndex))
            {
                // Add an outcome that routes to the convergence step after all branches complete.
                // Null value = matches any outcome (default routing after Branch completes).
                containerStep.Outcomes.Add(new ValueOutcome { NextStep = convergenceIndex });
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
