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
        }

        var bindingData = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["trigger"] = data.TriggerOutput,
            ["steps"] = stepsDict,
        };

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
    /// JSON object strings are parsed into dictionaries for nested path traversal.
    /// </summary>
    private static object? ResolveIterationItem(object? item)
    {
        if (item is not string jsonString || string.IsNullOrWhiteSpace(jsonString))
        {
            return item;
        }

        // Try to parse as a JSON object so ${loop.item.property} path traversal works.
        try
        {
            using var doc = JsonDocument.Parse(jsonString);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                return Dispatch.JsonOptions.DeserializeToUnwrappedDictionary(jsonString);
            }
        }
        catch (JsonException)
        {
            // Not JSON — return as-is (simple string value).
        }

        return item;
    }
}
