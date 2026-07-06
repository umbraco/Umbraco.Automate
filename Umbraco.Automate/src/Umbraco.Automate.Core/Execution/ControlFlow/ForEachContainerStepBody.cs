using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Conditions;
using Umbraco.Automate.Core.ControlFlow.BuiltIn;
using Umbraco.Automate.Core.Runs;
using WorkflowCore.Interface;
using WorkflowCore.Models;
using WorkflowCore.Primitives;

namespace Umbraco.Automate.Core.Execution.ControlFlow;

/// <summary>
/// WorkflowCore step body for ForEach container control flow.
/// Evaluates a collection binding expression and branches child steps for each item.
/// Mirrors the re-invocation pattern of <see cref="WorkflowCore.Primitives.Foreach"/>:
/// branch with <see cref="IteratorPersistenceData"/>, then on each re-entry use
/// <see cref="WorkflowInstance.IsBranchComplete"/> to wait for descendants to drain.
/// </summary>
internal sealed class ForEachContainerStepBody : ContainerStepBody
{
    private readonly StepConfiguration _stepConfig;
    private readonly ForEachControlFlowSettings _settings;
    private readonly ForEachCollectionCache _collectionCache;
    private readonly StepOutputHydrationCache _hydrationCache;
    private readonly ConditionEvaluator _conditionEvaluator;
    private readonly IAutomationRunRepository _runRepository;
    private readonly IReadOnlyList<ContainerBranchEdge> _branchEdges;

    public ForEachContainerStepBody(
        StepConfiguration stepConfig,
        ForEachControlFlowSettings settings,
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

        // Stash the collection expression so binding-time item resolution can re-materialise
        // the collection on a cache miss (iteration contexts carry only an index).
        data.ContainerCollections[_stepConfig.Id] = _settings.Collection;

        // Re-entry path. WorkflowCore re-invokes the container after spawning children;
        // wait for the entire body branch (including outcome-chain successors, which
        // inherit the container's Scope and so are covered by IsBranchComplete) to drain
        // before deciding what to do next. ExecutionResult.Persist keeps the parent
        // pointer Active without setting SleepUntil — the executor naturally re-runs
        // it the moment the last descendant completes.
        if (context.PersistenceData is IteratorPersistenceData persistence && persistence.ChildrenActive)
        {
            if (!context.Workflow.IsBranchComplete(context.ExecutionPointer.Id))
            {
                return ExecutionResult.Persist(persistence);
            }

            if (_settings.RunParallel)
            {
                // All iterations drained together — their scoped outputs can never be
                // read again, so prune them to stop the persisted blob growing.
                IterationScopePruner.PruneContainerScopes(data, _stepConfig.Id, parentIteration);
                _collectionCache.EvictCollection(data.RunId, _stepConfig.Id, parentIteration?.ScopePath);
                return ExecutionResult.Next();
            }

            // The just-drained iteration's scoped outputs are dead — prune before moving on.
            IterationScopePruner.PruneIterationScope(
                data,
                new ForEachIterationContext(null, persistence.Index, _stepConfig.Id, parentIteration).ScopePath);

            // Sequential: advance to the next passing index.
            var items = _collectionCache.GetOrMaterializeCollection(data, _stepConfig.Id, parentIteration, _settings.Collection);
            var nextIndex = FindNextPassingIndex(data, items, persistence.Index + 1, parentIteration);
            if (nextIndex < 0)
            {
                _collectionCache.EvictCollection(data.RunId, _stepConfig.Id, parentIteration?.ScopePath);
                return ExecutionResult.Next();
            }

            return ExecutionResult.Branch(
                [new ForEachIterationContext(null, nextIndex, _stepConfig.Id, parentIteration)],
                new IteratorPersistenceData { ChildrenActive = true, Index = nextIndex });
        }

        // First entry — materialise the collection and branch.
        var initialItems = _collectionCache.GetOrMaterializeCollection(data, _stepConfig.Id, parentIteration, _settings.Collection);

        if (initialItems.Count == 0)
        {
            _collectionCache.EvictCollection(data.RunId, _stepConfig.Id, parentIteration?.ScopePath);
            TrackStepRun(data, context.CancellationToken, iterationIndex: null, iterationTotal: 0);
            return ExecutionResult.Next();
        }

        if (_settings.RunParallel)
        {
            // Filter items whose outgoing-edge filters all fail. The branch entries themselves
            // (step.Children) cannot be filtered per-item — WorkflowCore cross-products
            // BranchValues × Children — so the filter gates the iteration as a whole. For the
            // common single-entry case this is the natural "skip this item" behaviour.
            var passing = initialItems
                .Select((item, index) => (Item: item, Index: index))
                .Where(t => AnyEdgePassesForItem(data, t.Item, t.Index, parentIteration))
                .ToList();

            TrackStepRun(data, context.CancellationToken, iterationIndex: null, iterationTotal: initialItems.Count);

            if (passing.Count == 0)
            {
                _collectionCache.EvictCollection(data.RunId, _stepConfig.Id, parentIteration?.ScopePath);
                return ExecutionResult.Next();
            }

            var branches = passing
                .Select(t => (object)new ForEachIterationContext(null, t.Index, _stepConfig.Id, parentIteration))
                .ToList();
            return ExecutionResult.Branch(branches, new IteratorPersistenceData { ChildrenActive = true });
        }

        // Sequential — branch the first passing item.
        var firstIndex = FindNextPassingIndex(data, initialItems, 0, parentIteration);
        if (firstIndex < 0)
        {
            _collectionCache.EvictCollection(data.RunId, _stepConfig.Id, parentIteration?.ScopePath);
            TrackStepRun(data, context.CancellationToken, iterationIndex: null, iterationTotal: initialItems.Count);
            return ExecutionResult.Next();
        }

        TrackStepRun(data, context.CancellationToken, iterationIndex: firstIndex, iterationTotal: initialItems.Count);
        return ExecutionResult.Branch(
            [new ForEachIterationContext(null, firstIndex, _stepConfig.Id, parentIteration)],
            new IteratorPersistenceData { ChildrenActive = true, Index = firstIndex });
    }

    private int FindNextPassingIndex(AutomationWorkflowData data, IReadOnlyList<object?> items, int from, ForEachIterationContext? parent)
    {
        for (var i = from; i < items.Count; i++)
        {
            if (AnyEdgePassesForItem(data, items[i], i, parent))
            {
                return i;
            }
        }
        return -1;
    }

    private bool AnyEdgePassesForItem(AutomationWorkflowData data, object? item, int index, ForEachIterationContext? parent)
    {
        // Skip per-item binding-data construction when no edge gates the branch.
        if (_branchEdges.Count == 0 || !ContainerBranchEdge.AnyHaveFilter(_branchEdges))
        {
            return true;
        }

        // Transient context carrying the in-hand item — never branched, so the item is
        // not persisted anywhere.
        var iterationContext = new ForEachIterationContext(item, index, _stepConfig.Id, parent);
        var bindingData = BindingDataBuilder.Build(data, iterationContext, _collectionCache, _hydrationCache);
        return ContainerBranchEdge.AnyEdgePasses(_branchEdges, _conditionEvaluator, bindingData);
    }

    private void TrackStepRun(AutomationWorkflowData data, CancellationToken ct, int? iterationIndex, int iterationTotal)
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
            IterationIndex = iterationIndex,
            IterationTotal = iterationTotal,
        };
        _runRepository.SaveStepRunAsync(stepRun, ct).GetAwaiter().GetResult();
    }
}
