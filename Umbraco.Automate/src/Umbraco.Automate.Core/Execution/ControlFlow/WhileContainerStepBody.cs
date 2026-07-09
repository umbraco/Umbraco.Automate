using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Conditions;
using Umbraco.Automate.Core.ControlFlow.BuiltIn;
using Umbraco.Automate.Core.Runs;
using WorkflowCore.Interface;
using WorkflowCore.Models;
using WorkflowCore.Primitives;

namespace Umbraco.Automate.Core.Execution.ControlFlow;

/// <summary>
/// WorkflowCore step body for While container control flow. Re-evaluates conditions
/// before each iteration and waits for the previous iteration's body to drain via
/// <see cref="WorkflowInstance.IsBranchComplete"/> before deciding to loop again.
/// Includes a max iteration safety guard.
/// </summary>
internal sealed class WhileContainerStepBody : ContainerStepBody
{
    private readonly StepConfiguration _stepConfig;
    private readonly WhileControlFlowSettings _settings;
    private readonly ForEachCollectionCache _collectionCache;
    private readonly StepOutputHydrationCache _hydrationCache;
    private readonly ConditionEvaluator _conditionEvaluator;
    private readonly IAutomationRunRepository _runRepository;
    private readonly IReadOnlyList<ContainerBranchEdge> _branchEdges;

    public WhileContainerStepBody(
        StepConfiguration stepConfig,
        WhileControlFlowSettings settings,
        ForEachCollectionCache collectionCache,
        StepOutputHydrationCache hydrationCache,
        ConditionEvaluator conditionEvaluator,
        IAutomationRunRepository runRepository,
        IReadOnlyList<ContainerBranchEdge> branchEdges)
    {
        _stepConfig = stepConfig;
        _settings = settings;
        _collectionCache = collectionCache;
        _hydrationCache = hydrationCache;
        _conditionEvaluator = conditionEvaluator;
        _runRepository = runRepository;
        _branchEdges = branchEdges;
    }

    public override ExecutionResult Run(IStepExecutionContext context)
    {
        var data = (AutomationWorkflowData)context.Workflow.Data;
        var parentIteration = context.Item as ForEachIterationContext;

        // Re-entry: a previous iteration's body must drain before we re-evaluate the
        // condition for the next pass. IteratorPersistenceData.Index counts iterations
        // already branched.
        var iteration = 0;
        if (context.PersistenceData is IteratorPersistenceData persistence && persistence.ChildrenActive)
        {
            if (!context.Workflow.IsBranchComplete(context.ExecutionPointer.Id))
            {
                return ExecutionResult.Persist(persistence);
            }
            iteration = persistence.Index;

            // The just-drained iteration's scoped outputs are dead — prune before
            // deciding whether to loop again.
            if (iteration > 0)
            {
                IterationScopePruner.PruneIterationScope(
                    data,
                    new ForEachIterationContext(null, iteration - 1, _stepConfig.Id, parentIteration).ScopePath);
            }
        }

        if (iteration >= _settings.MaxIterations)
        {
            TrackStepRun(data, context.CancellationToken, iteration);
            return ExecutionResult.Next();
        }

        var bindingData = BindingDataBuilder.Build(data, parentIteration, _collectionCache, _hydrationCache, context.CancellationToken);

        // Evaluate While's own conditions.
        if (!_conditionEvaluator.Evaluate(_settings.Conditions, bindingData))
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

        return ExecutionResult.Branch(
            [new ForEachIterationContext(null, iteration, _stepConfig.Id, parentIteration)],
            new IteratorPersistenceData { ChildrenActive = true, Index = iteration + 1 });
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
        _runRepository.AddStepRunAsync(stepRun, ct).GetAwaiter().GetResult();
    }
}
