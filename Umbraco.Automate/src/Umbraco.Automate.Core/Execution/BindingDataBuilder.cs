using System.Text.Json;
using Umbraco.Automate.Core.Execution.ControlFlow;

namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// Builds the binding data dictionary from workflow data for use in condition evaluation
/// and binding resolution. Shared by <see cref="ActionStepBody"/> and control flow step bodies.
/// </summary>
internal static class BindingDataBuilder
{
    /// <summary>
    /// Builds a binding data dictionary containing trigger output and accumulated step outputs.
    /// </summary>
    /// <param name="data">The workflow data carrying per-run state.</param>
    /// <param name="iterationContext">Optional ForEach iteration context for child steps inside a loop.</param>
    /// <returns>A dictionary suitable for binding evaluation.</returns>
    public static Dictionary<string, object?> Build(AutomationWorkflowData data, ForEachIterationContext? iterationContext = null)
    {
        var stepsDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (stepId, outputs) in data.StepOutputs)
        {
            stepsDict[stepId.ToString()] = outputs;

            // Also register by alias so both ${steps.GUID.field} and ${steps.alias.field} work.
            if (data.StepAliases.TryGetValue(stepId, out var alias))
            {
                stepsDict[alias] = outputs;
            }
        }

        var bindingData = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["trigger"] = data.TriggerOutput,
            ["steps"] = stepsDict,
        };

        // Add "previous" key pointing to the last completed step's outputs.
        if (data.LastCompletedStepId.HasValue
            && data.StepOutputs.TryGetValue(data.LastCompletedStepId.Value, out var previousOutputs))
        {
            bindingData["previous"] = previousOutputs;
        }

        if (iterationContext is not null)
        {
            bindingData["loop"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["item"] = ResolveIterationItem(iterationContext.Item),
                ["index"] = iterationContext.Index,
            };
        }

        return bindingData;
    }

    /// <summary>
    /// Resolves a ForEach iteration item to a binding-compatible value.
    /// JSON strings are parsed into structured types (dictionaries, lists, or primitives)
    /// for nested path traversal. This remains necessary as a defence against the
    /// Newtonsoft.Json round-trip in WorkflowCore persistence, which can turn structured
    /// data back into JSON strings after workflow suspension and resumption.
    /// </summary>
    private static object? ResolveIterationItem(object? item)
    {
        if (item is not string jsonString || string.IsNullOrWhiteSpace(jsonString))
        {
            return item;
        }

        try
        {
            using var doc = JsonDocument.Parse(jsonString);
            return Dispatch.JsonOptions.UnwrapJsonElement(doc.RootElement);
        }
        catch (JsonException)
        {
            // Not JSON — return as-is (simple string value).
        }

        return item;
    }
}
