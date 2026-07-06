using System.Text;
using Umbraco.Automate.Core.Configuration;

namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// Creates and detects the reference marker stored in <see cref="AutomationWorkflowData.StepOutputs"/>
/// (and <see cref="AutomationWorkflowData.IterationStepOutputs"/>) in place of a step's inline
/// output when the serialized output exceeds <see cref="ExecutionOptions.MaxInlineOutputBytes"/>.
/// The workflow data is re-serialized by WorkflowCore on every execution pass, so large outputs
/// live only on the step run record (written once) and are hydrated on demand at binding time.
/// </summary>
internal static class StepOutputReference
{
    /// <summary>
    /// The single, namespaced key of a marker dictionary. A dictionary is a marker only when
    /// this is its <em>sole</em> key and the value parses as a <see cref="Guid"/>, so real step
    /// outputs (which carry their own fields) can never be mistaken for one.
    /// </summary>
    public const string MarkerKey = "$automateOutputRef";

    /// <summary>
    /// Returns the step output dictionary to inline into the workflow data: the unwrapped
    /// output itself when its size is at or below <paramref name="maxInlineBytes"/>, or a
    /// marker referencing <paramref name="stepRunId"/> when larger. Size is measured as the
    /// UTF-8 encoded byte count of <paramref name="outputJson"/>.
    /// </summary>
    public static Dictionary<string, object?> CreateInlineOrMarker(string outputJson, Guid stepRunId, int maxInlineBytes)
        => Encoding.UTF8.GetByteCount(outputJson) <= maxInlineBytes
            ? Dispatch.JsonOptions.DeserializeToUnwrappedDictionary(outputJson)
            : CreateMarker(stepRunId);

    /// <summary>
    /// Creates a marker dictionary referencing the given step run. The id is stored as a
    /// string (not a <see cref="Guid"/>) because the Newtonsoft <c>TypeNameHandling.All</c>
    /// round-trip used by the WorkflowCore persistence provider has no type metadata for
    /// primitive dictionary values — a <see cref="Guid"/> would come back as a string anyway,
    /// so we store the canonical form deliberately.
    /// </summary>
    public static Dictionary<string, object?> CreateMarker(Guid stepRunId)
        => new(StringComparer.OrdinalIgnoreCase) { [MarkerKey] = stepRunId.ToString("D") };

    /// <summary>
    /// Detects a marker dictionary and extracts the step run id it references.
    /// </summary>
    public static bool TryGetStepRunId(IReadOnlyDictionary<string, object?>? outputs, out Guid stepRunId)
    {
        stepRunId = default;
        return outputs is { Count: 1 }
            && outputs.TryGetValue(MarkerKey, out var value)
            && Guid.TryParse(value as string, out stepRunId);
    }
}
