using System.Collections.Concurrent;
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
/// entries for runs abandoned mid-flight are flushed by the <see cref="MaxEntries"/> backstop.
/// </summary>
internal sealed class StepOutputHydrationCache
{
    /// <summary>
    /// Backstop bound: hydrated outputs are large by definition (they exceeded the inline
    /// threshold), so rather than tracking recency we flush the whole cache when it fills —
    /// subsequent binds simply re-hydrate from the database.
    /// </summary>
    internal const int MaxEntries = 64;

    private static readonly IReadOnlyDictionary<string, object?> EmptyOutputs =
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    private readonly IAutomationRunRepository _runRepository;
    private readonly ConcurrentDictionary<(Guid RunId, Guid StepRunId), IReadOnlyDictionary<string, object?>> _outputs = new();

    public StepOutputHydrationCache(IAutomationRunRepository runRepository)
    {
        _runRepository = runRepository;
    }

    /// <summary>
    /// Gets the hydrated output dictionary for a step run, fetching and memoising it on first
    /// access. Returns an empty dictionary when the step run row is gone or has no output.
    /// </summary>
    public IReadOnlyDictionary<string, object?> GetOutput(Guid runId, Guid stepRunId)
    {
        var key = (runId, stepRunId);
        if (_outputs.TryGetValue(key, out var cached))
        {
            return cached;
        }

        // Sync-over-async: hydration happens inside synchronous binding-path traversal.
        // Precedent: ForEachContainerStepBody.TrackStepRun.
        var outputJson = _runRepository.GetStepRunOutputAsync(stepRunId).GetAwaiter().GetResult();
        var outputs = outputJson is null
            ? EmptyOutputs
            : Dispatch.JsonOptions.DeserializeToUnwrappedDictionary(outputJson);

        if (_outputs.Count >= MaxEntries)
        {
            _outputs.Clear();
        }

        return _outputs.GetOrAdd(key, outputs);
    }

    /// <summary>
    /// Evicts every cached output belonging to a run once it reaches a terminal state.
    /// </summary>
    public void EvictRun(Guid runId)
    {
        foreach (var key in _outputs.Keys.Where(k => k.RunId == runId))
        {
            _outputs.TryRemove(key, out _);
        }
    }
}
