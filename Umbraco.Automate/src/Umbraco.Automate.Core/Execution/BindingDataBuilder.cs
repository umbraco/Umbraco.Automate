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
    /// <returns>A dictionary suitable for binding evaluation.</returns>
    public static Dictionary<string, object?> Build(AutomationWorkflowData data)
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

        return bindingData;
    }
}
