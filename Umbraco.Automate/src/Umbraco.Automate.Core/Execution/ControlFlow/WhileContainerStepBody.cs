using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Conditions;
using Umbraco.Automate.Core.ControlFlow.BuiltIn;
using Umbraco.Automate.Core.Runs;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace Umbraco.Automate.Core.Execution.ControlFlow;

/// <summary>
/// WorkflowCore step body for While container control flow.
/// Evaluates conditions before each iteration, branches children while true.
/// Includes a max iteration safety guard.
/// </summary>
internal sealed class WhileContainerStepBody : StepBody
{
    private readonly StepConfiguration _stepConfig;
    private readonly WhileControlFlowSettings _settings;
    private readonly ConditionEvaluator _conditionEvaluator;
    private readonly IAutomationRunRepository _runRepository;
    private readonly IReadOnlyList<ContainerBranchEdge> _branchEdges;

    public WhileContainerStepBody(
        StepConfiguration stepConfig,
        WhileControlFlowSettings settings,
        ConditionEvaluator conditionEvaluator,
        IAutomationRunRepository runRepository,
        IReadOnlyList<ContainerBranchEdge> branchEdges)
    {
        _stepConfig = stepConfig;
        _settings = settings;
        _conditionEvaluator = conditionEvaluator;
        _runRepository = runRepository;
        _branchEdges = branchEdges;
    }

    public override ExecutionResult Run(IStepExecutionContext context)
    {
        var data = (AutomationWorkflowData)context.Workflow.Data;
        var bindingData = BindingDataBuilder.Build(data);

        // Track iteration count.
        var iteration = 0;
        if (context.PersistenceData is ControlFlowPersistenceData persistence &&
            persistence.Metadata is not null &&
            int.TryParse(persistence.Metadata, out var parsedIteration))
        {
            iteration = parsedIteration;
        }

        // Safety guard.
        if (iteration >= _settings.MaxIterations)
        {
            TrackStepRun(data, context.CancellationToken, iteration);
            return ExecutionResult.Next();
        }

        // Evaluate While's own conditions.
        var conditionsTrue = _conditionEvaluator.Evaluate(_settings.Conditions, bindingData);
        if (!conditionsTrue)
        {
            TrackStepRun(data, context.CancellationToken, iteration);
            return ExecutionResult.Next();
        }

        // Evaluate outgoing-edge filters once with workflow data. While does not expose
        // a per-item iteration value, so loop.* is unavailable here. If every edge has
        // a filter and they all fail, treat the loop as done — the alternative (looping
        // forever on filters that never become true) would be worse than terminating.
        if (!ContainerBranchEdge.AnyEdgePasses(_branchEdges, _conditionEvaluator, bindingData))
        {
            TrackStepRun(data, context.CancellationToken, iteration);
            return ExecutionResult.Next();
        }

        // Branch children for this iteration.
        var nextIteration = iteration + 1;
        return ExecutionResult.Branch(
            [new ForEachIterationContext(null, iteration)],
            new ControlFlowPersistenceData(nameof(WhileContainerStepBody)) { Metadata = nextIteration.ToString() });
    }

    private void TrackStepRun(AutomationWorkflowData data, CancellationToken ct, int totalIterations)
    {
        var stepRun = new StepRun
        {
            Id = Guid.NewGuid(),
            RunId = data.RunId,
            StepId = _stepConfig.Id,
            ActionAlias = _stepConfig.ActionAlias,
            Status = StepRunStatus.Completed,
            StartedUtc = DateTime.UtcNow,
            CompletedUtc = DateTime.UtcNow,
            IterationTotal = totalIterations,
        };
        _runRepository.SaveStepRunAsync(stepRun, ct).GetAwaiter().GetResult();
    }
}
