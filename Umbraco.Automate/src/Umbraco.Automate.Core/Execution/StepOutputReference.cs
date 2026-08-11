using System.Text;
using Umbraco.Automate.Core.Configuration;

namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// Creates and detects the reference markers stored in <see cref="AutomationWorkflowData.StepOutputs"/>
/// (and <see cref="AutomationWorkflowData.IterationStepOutputs"/>) in place of a step's inline
/// output, and in <see cref="AutomationWorkflowData.TriggerOutput"/> in place of the trigger's
/// inline output, when the serialized output exceeds
/// <see cref="ExecutionOptions.MaxInlineOutputBytes"/>. The workflow data is re-serialized by
/// WorkflowCore on every execution pass, so large outputs live only on the record that already
/// holds them once — <c>StepRun.OutputData</c> for a step, <c>AutomationRun.TriggerData</c> for
/// the trigger — and are hydrated on demand at binding time.
/// </summary>
internal static class StepOutputReference
{
    /// <summary>
    /// The single, namespaced key of a step-output marker dictionary. A dictionary is a marker
    /// only when this is its <em>sole</em> key and the value parses as a <see cref="Guid"/>, so
    /// real step outputs (which carry their own fields) can never be mistaken for one.
    /// </summary>
    public const string MarkerKey = "$automateOutputRef";

    /// <summary>
    /// The single, namespaced key of a trigger-output marker dictionary. Deliberately distinct
    /// from <see cref="MarkerKey"/>: a trigger marker references the <em>run</em> (whose
    /// <c>TriggerData</c> holds the payload), not a step run, so the two must never be
    /// confused for one another when detected.
    /// </summary>
    public const string TriggerMarkerKey = "$automateTriggerOutputRef";

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
    /// Returns the trigger output dictionary to inline into the workflow data: the dictionary
    /// itself when its serialized size is at or below <paramref name="maxInlineBytes"/>, or a
    /// marker referencing <paramref name="runId"/> when larger. Size is measured as the UTF-8
    /// encoded byte count of <paramref name="triggerJson"/>.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="CreateInlineOrMarker"/> this takes the already-materialised dictionary
    /// and inlines it untouched rather than re-deserializing <paramref name="triggerJson"/>. The
    /// trigger path starts from a dictionary (the dispatcher already unwrapped it), so a
    /// serialize/deserialize round-trip here would be pure cost and could subtly change value
    /// types on the inline path that every existing run takes.
    /// </remarks>
    public static Dictionary<string, object?> CreateInlineOrTriggerMarker(
        Dictionary<string, object?> triggerOutput,
        string triggerJson,
        Guid runId,
        int maxInlineBytes)
        => Encoding.UTF8.GetByteCount(triggerJson) <= maxInlineBytes
            ? triggerOutput
            : CreateTriggerMarker(runId);

    /// <summary>
    /// Creates a marker dictionary referencing the given run's <c>TriggerData</c>. The id is
    /// stored as a string for the same reason as <see cref="CreateMarker"/>.
    /// </summary>
    public static Dictionary<string, object?> CreateTriggerMarker(Guid runId)
        => new(StringComparer.OrdinalIgnoreCase) { [TriggerMarkerKey] = runId.ToString("D") };

    /// <summary>
    /// Detects a step-output marker dictionary and extracts the step run id it references.
    /// </summary>
    public static bool TryGetStepRunId(IReadOnlyDictionary<string, object?>? outputs, out Guid stepRunId)
        => TryGetMarkerId(outputs, MarkerKey, out stepRunId);

    /// <summary>
    /// Detects a trigger-output marker dictionary and extracts the run id it references.
    /// </summary>
    public static bool TryGetTriggerRunId(IReadOnlyDictionary<string, object?>? triggerOutput, out Guid runId)
        => TryGetMarkerId(triggerOutput, TriggerMarkerKey, out runId);

    private static bool TryGetMarkerId(IReadOnlyDictionary<string, object?>? outputs, string markerKey, out Guid id)
    {
        id = default;
        return outputs is { Count: 1 }
            && outputs.TryGetValue(markerKey, out var value)
            && Guid.TryParse(value as string, out id);
    }
}
