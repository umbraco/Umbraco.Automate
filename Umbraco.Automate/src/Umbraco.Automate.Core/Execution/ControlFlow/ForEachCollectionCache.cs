using System.Collections.Concurrent;
using System.Text.Json;
using Umbraco.Automate.Core.Bindings;

namespace Umbraco.Automate.Core.Execution.ControlFlow;

/// <summary>
/// Per-process cache of materialised ForEach collections, keyed per run, container and
/// enclosing iteration scope. Iteration contexts branched into WorkflowCore carry only an
/// index (embedding the item would persist it into every body-step pointer, growing the
/// workflow blob O(n²)); binding-time resolution looks the item up here instead.
/// On a cache miss — e.g. resuming a run after a process restart — the collection expression
/// stashed in <see cref="AutomationWorkflowData.ContainerCollections"/> is re-evaluated
/// against the enclosing iteration's binding data, which is the same evaluation the container
/// performed on every sequential re-entry before this cache existed.
/// Entries are evicted when their container completes, and <see cref="EvictRun"/> sweeps any
/// stragglers when the run reaches a terminal WorkflowCore status (see
/// <see cref="Umbraco.Automate.Core.Execution.RunFinalizer"/>), so entries for runs
/// abandoned mid-loop are bounded to the remaining lifetime of that run rather than the
/// process.
/// </summary>
internal sealed class ForEachCollectionCache
{
    private readonly BindingEvaluator _bindingEvaluator;
    private readonly StepOutputHydrationCache _hydrationCache;
    private readonly ConcurrentDictionary<CollectionKey, IReadOnlyList<object?>> _collections = new();

    public ForEachCollectionCache(BindingEvaluator bindingEvaluator, StepOutputHydrationCache hydrationCache)
    {
        _bindingEvaluator = bindingEvaluator;
        _hydrationCache = hydrationCache;
    }

    /// <summary>
    /// Gets the materialised collection for a container within the given enclosing iteration,
    /// evaluating and materialising the collection expression on first access.
    /// </summary>
    /// <remarks>
    /// Behaviour change: the collection is evaluated once per run and reused for every
    /// sequential re-entry (previously it was re-evaluated against live data on every
    /// re-entry). If the collection expression reads data that the loop body itself
    /// mutates, later iterations will keep seeing the value captured on first access, not
    /// the mutation.
    /// </remarks>
    public IReadOnlyList<object?> GetOrMaterializeCollection(
        AutomationWorkflowData data,
        Guid containerStepId,
        ForEachIterationContext? parentIteration,
        string collectionExpression,
        CancellationToken cancellationToken = default)
    {
        var key = new CollectionKey(data.RunId, containerStepId, parentIteration?.ScopePath);
        if (_collections.TryGetValue(key, out var items))
        {
            return items;
        }

        // Evaluate against the enclosing iteration's binding data — for nested loops the
        // expression may reference the parent's loop.item, which recurses through this
        // cache and terminates at the outermost loop.
        var bindingData = BindingDataBuilder.Build(data, parentIteration, this, _hydrationCache, cancellationToken);
        items = MaterializeCollection(_bindingEvaluator.Evaluate(collectionExpression, bindingData));
        return _collections.GetOrAdd(key, items);
    }

    /// <summary>
    /// Resolves the item an iteration context points at, or <c>null</c> when the container
    /// exposes no collection (While and Parallel containers, whose iterations carry no item).
    /// </summary>
    public object? ResolveItem(AutomationWorkflowData data, ForEachIterationContext iterationContext, CancellationToken cancellationToken = default)
    {
        if (!data.ContainerCollections.TryGetValue(iterationContext.ContainerStepId, out var collectionExpression))
        {
            return null;
        }

        var items = GetOrMaterializeCollection(data, iterationContext.ContainerStepId, iterationContext.Parent, collectionExpression, cancellationToken);
        return iterationContext.Index >= 0 && iterationContext.Index < items.Count
            ? items[iterationContext.Index]
            : null;
    }

    /// <summary>
    /// Evicts a container's materialised collection once its loop has completed.
    /// </summary>
    public void EvictCollection(Guid runId, Guid containerStepId, string? parentScopePath)
        => _collections.TryRemove(new CollectionKey(runId, containerStepId, parentScopePath), out _);

    /// <summary>
    /// Evicts every cached collection for a run, regardless of container or iteration scope.
    /// Called once a run reaches a terminal WorkflowCore status, so entries for loops that
    /// never reached their own natural pruning point (abandoned or terminated mid-loop) don't
    /// leak for the lifetime of the process.
    /// </summary>
    public void EvictRun(Guid runId)
    {
        foreach (var key in _collections.Keys.Where(k => k.RunId == runId))
        {
            _collections.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Materialises an evaluated collection value: a JSON array is deep-converted to plain
    /// .NET types; anything else is treated as comma-separated values.
    /// </summary>
    internal static List<object?> MaterializeCollection(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        // Try to parse as JSON array.
        try
        {
            using var doc = JsonDocument.Parse(value);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                return doc.RootElement.EnumerateArray()
                    .Select(e => Dispatch.JsonOptions.UnwrapJsonElement(e))
                    .ToList();
            }
        }
        catch (JsonException)
        {
            // Not JSON — treat as comma-separated.
        }

        // Fall back to comma-separated values.
        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Cast<object?>()
            .ToList();
    }

    private readonly record struct CollectionKey(Guid RunId, Guid ContainerStepId, string? ParentScopePath);
}
