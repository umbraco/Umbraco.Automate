using System.Collections;

namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// Lazy stand-in that <see cref="BindingDataBuilder"/> substitutes for a
/// <see cref="StepOutputReference"/> marker in the binding data — for an offloaded step output
/// or an offloaded trigger output alike. Construction is free; the first member access hydrates
/// the full output through <see cref="StepOutputHydrationCache"/>, so a run that never binds into
/// an offloaded output pays zero hydration reads.
/// Implements <see cref="IReadOnlyDictionary{TKey,TValue}"/> so <c>BindingEvaluator</c> path
/// traversal (and its JSON stringification of whole-output binds) sees an ordinary dictionary.
/// </summary>
internal sealed class OffloadedStepOutput : IReadOnlyDictionary<string, object?>
{
    private readonly Lazy<IReadOnlyDictionary<string, object?>> _outputs;

    private OffloadedStepOutput(Func<IReadOnlyDictionary<string, object?>> hydrate)
    {
        _outputs = new Lazy<IReadOnlyDictionary<string, object?>>(hydrate);
    }

    /// <summary>
    /// A stand-in for a step's offloaded output, hydrated from the step run record.
    /// </summary>
    public static OffloadedStepOutput ForStepRun(
        StepOutputHydrationCache hydrationCache,
        Guid runId,
        Guid stepRunId,
        CancellationToken cancellationToken = default)
        => new(() => hydrationCache.GetOutput(runId, stepRunId, cancellationToken));

    /// <summary>
    /// A stand-in for a run's offloaded trigger output, hydrated from the run's trigger data.
    /// </summary>
    public static OffloadedStepOutput ForTrigger(
        StepOutputHydrationCache hydrationCache,
        Guid runId,
        CancellationToken cancellationToken = default)
        => new(() => hydrationCache.GetTriggerOutput(runId, cancellationToken));

    public object? this[string key] => _outputs.Value[key];

    public IEnumerable<string> Keys => _outputs.Value.Keys;

    public IEnumerable<object?> Values => _outputs.Value.Values;

    public int Count => _outputs.Value.Count;

    public bool ContainsKey(string key) => _outputs.Value.ContainsKey(key);

    public bool TryGetValue(string key, out object? value) => _outputs.Value.TryGetValue(key, out value);

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => _outputs.Value.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
