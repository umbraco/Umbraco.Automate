namespace Umbraco.Automate.Core.Execution.ControlFlow;

/// <summary>
/// Removes drained iteration scopes from <see cref="AutomationWorkflowData.IterationStepOutputs"/>
/// and <see cref="AutomationWorkflowData.IterationLastCompletedStepId"/>. Without pruning, every
/// iteration of a loop leaves its scoped outputs in the workflow data forever, growing the
/// persisted workflow blob linearly with iteration count. Pruning is safe because
/// <see cref="BindingDataBuilder"/> only ever reads the current iteration context's scope path
/// and its ancestors — a scope whose branch has fully drained is never read again.
/// </summary>
internal static class IterationScopePruner
{
    /// <summary>
    /// Prunes a single completed iteration's scope, including descendant scopes produced by
    /// containers nested inside that iteration (their scope paths share the iteration's
    /// scope path as a <c>/</c>-separated prefix).
    /// </summary>
    public static void PruneIterationScope(AutomationWorkflowData data, string scopePath)
    {
        var descendantPrefix = scopePath + "/";
        Prune(data, key => key == scopePath || key.StartsWith(descendantPrefix, StringComparison.Ordinal));
    }

    /// <summary>
    /// Prunes every iteration scope a container produced — all indices and their descendants.
    /// Used when a parallel container's branches drain together and complete as one.
    /// </summary>
    public static void PruneContainerScopes(AutomationWorkflowData data, Guid containerStepId, ForEachIterationContext? parentIteration)
    {
        var prefix = parentIteration is null
            ? $"{containerStepId:N}:"
            : $"{parentIteration.ScopePath}/{containerStepId:N}:";
        Prune(data, key => key.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static void Prune(AutomationWorkflowData data, Func<string, bool> matches)
    {
        foreach (var key in data.IterationStepOutputs.Keys.Where(matches).ToList())
        {
            data.IterationStepOutputs.Remove(key);
        }

        foreach (var key in data.IterationLastCompletedStepId.Keys.Where(matches).ToList())
        {
            data.IterationLastCompletedStepId.Remove(key);
        }
    }
}
