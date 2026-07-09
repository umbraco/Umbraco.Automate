using Umbraco.Automate.Core.Runs;

namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// Per-process cache of hydrated offloaded step outputs, keyed by run and step run. When a
/// binding touches a <see cref="StepOutputReference"/> marker, the full output is fetched from
/// the StepRun table (multi-node safe — hydration always goes through the database, never
/// assumes the output was produced on this node) and memoised so repeated binds within a run
/// don't hammer the database. Misses (step run deleted by retention cleanup, or no output)
/// are memoised as empty so the path resolves like an unknown step instead of throwing.
/// Entries are evicted when their run reaches a terminal state (see <c>RunFinalizer</c>);
/// entries for runs abandoned mid-flight are bounded by the <see cref="MaxEntries"/> LRU
/// eviction below, which only ever drops the least-recently-touched entry rather than the
/// whole cache — several concurrently-running automations can each keep a hot entry without
/// evicting one another.
/// </summary>
/// <remarks>
/// Hydration is sync-over-async (<c>GetAwaiter().GetResult()</c>) because
/// <see cref="OffloadedStepOutput"/> — the lazy stand-in this cache backs — implements the
/// synchronous <see cref="IReadOnlyDictionary{TKey,TValue}"/> contract that the generic,
/// non-async binding path-traversal engine (<c>BindingEvaluator.ResolvePath</c>) requires.
/// That engine, and its caller <c>BindingDataBuilder.Build</c>, are invoked from dozens of
/// synchronous call sites across the codebase (every action's settings resolution via
/// <c>SettingsBindingResolver</c>, <c>ConditionEvaluator</c>, <c>WorkflowCompiler</c>'s
/// <c>Func&lt;AutomationWorkflowData, object&gt;</c> outcome lambdas, and more) — turning
/// hydration fully async would mean making that whole traversal engine async and touching
/// every one of those call sites, several of which (e.g. the WorkflowCore outcome lambda
/// delegate shape) cannot accept a <see cref="Task"/> at all. That refactor was judged too
/// broad and risky to fold into this change; a <see cref="CancellationToken"/> is still
/// threaded through so the blocking read can at least be cancelled.
/// </remarks>
internal sealed class StepOutputHydrationCache
{
    /// <summary>
    /// Bound on the number of memoised outputs. Hydrated outputs are large by definition
    /// (they exceeded the inline threshold), so the cache tracks access recency and evicts
    /// only the least-recently-used entry once it fills, rather than flushing everything.
    /// </summary>
    internal const int MaxEntries = 64;

    private static readonly IReadOnlyDictionary<string, object?> EmptyOutputs =
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    private readonly IAutomationRunRepository _runRepository;
    private readonly object _lock = new();
    private readonly Dictionary<(Guid RunId, Guid StepRunId), LinkedListNode<CacheEntry>> _index = new();
    private readonly LinkedList<CacheEntry> _lruOrder = new();

    public StepOutputHydrationCache(IAutomationRunRepository runRepository)
    {
        _runRepository = runRepository;
    }

    /// <summary>
    /// Gets the hydrated output dictionary for a step run, fetching and memoising it on first
    /// access. Returns an empty dictionary when the step run row is gone or has no output.
    /// </summary>
    public IReadOnlyDictionary<string, object?> GetOutput(Guid runId, Guid stepRunId, CancellationToken cancellationToken = default)
    {
        var key = (runId, stepRunId);

        lock (_lock)
        {
            if (_index.TryGetValue(key, out var existingNode))
            {
                Touch(existingNode);
                return existingNode.Value.Outputs;
            }
        }

        // Sync-over-async: hydration happens inside synchronous binding-path traversal.
        // See the class remarks for why this remains sync-over-async rather than a fully
        // async call chain. Precedent: ForEachContainerStepBody.TrackStepRun.
        var outputJson = _runRepository.GetStepRunOutputAsync(stepRunId, runId, cancellationToken).GetAwaiter().GetResult();
        var outputs = outputJson is null
            ? EmptyOutputs
            : Dispatch.JsonOptions.DeserializeToUnwrappedDictionary(outputJson);

        lock (_lock)
        {
            // Another thread may have hydrated and inserted the same key while this thread
            // was awaiting the database — prefer the existing entry over adding a duplicate.
            if (_index.TryGetValue(key, out var racedNode))
            {
                Touch(racedNode);
                return racedNode.Value.Outputs;
            }

            if (_index.Count >= MaxEntries)
            {
                var leastRecentlyUsed = _lruOrder.Last;
                if (leastRecentlyUsed is not null)
                {
                    _lruOrder.RemoveLast();
                    _index.Remove(leastRecentlyUsed.Value.Key);
                }
            }

            var node = new LinkedListNode<CacheEntry>(new CacheEntry(key, outputs));
            _lruOrder.AddFirst(node);
            _index[key] = node;
        }

        return outputs;
    }

    /// <summary>
    /// Evicts every cached output belonging to a run once it reaches a terminal state.
    /// </summary>
    public void EvictRun(Guid runId)
    {
        lock (_lock)
        {
            foreach (var key in _index.Keys.Where(k => k.RunId == runId).ToList())
            {
                if (_index.Remove(key, out var node))
                {
                    _lruOrder.Remove(node);
                }
            }
        }
    }

    /// <summary>
    /// Moves a node to the most-recently-used end of the order list. Callers must hold
    /// <see cref="_lock"/>.
    /// </summary>
    private void Touch(LinkedListNode<CacheEntry> node)
    {
        _lruOrder.Remove(node);
        _lruOrder.AddFirst(node);
    }

    private readonly record struct CacheEntry((Guid RunId, Guid StepRunId) Key, IReadOnlyDictionary<string, object?> Outputs);
}
