using System.Collections;

namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// Lazy stand-in that <see cref="BindingDataBuilder"/> substitutes for a
/// <see cref="StepOutputReference"/> marker in the binding data. Construction is free; the
/// first member access hydrates the full output through <see cref="StepOutputHydrationCache"/>,
/// so a run that never binds into an offloaded output pays zero hydration reads.
/// Implements <see cref="IReadOnlyDictionary{TKey,TValue}"/> so <c>BindingEvaluator</c> path
/// traversal (and its JSON stringification of whole-output binds) sees an ordinary dictionary.
/// </summary>
internal sealed class OffloadedStepOutput : IReadOnlyDictionary<string, object?>
{
    private readonly Lazy<IReadOnlyDictionary<string, object?>> _outputs;

    public OffloadedStepOutput(StepOutputHydrationCache hydrationCache, Guid runId, Guid stepRunId, CancellationToken cancellationToken = default)
    {
        _outputs = new Lazy<IReadOnlyDictionary<string, object?>>(() => hydrationCache.GetOutput(runId, stepRunId, cancellationToken));
    }

    public object? this[string key] => _outputs.Value[key];

    public IEnumerable<string> Keys => _outputs.Value.Keys;

    public IEnumerable<object?> Values => _outputs.Value.Values;

    public int Count => _outputs.Value.Count;

    public bool ContainsKey(string key) => _outputs.Value.ContainsKey(key);

    public bool TryGetValue(string key, out object? value) => _outputs.Value.TryGetValue(key, out value);

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => _outputs.Value.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
