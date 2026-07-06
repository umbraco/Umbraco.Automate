namespace Umbraco.Automate.Core.Execution.ControlFlow;

/// <summary>
/// Context data passed to each iteration of a container loop (ForEach, While, Parallel).
/// Used as the branch value in <see cref="WorkflowCore.Models.ExecutionResult.Branch"/>,
/// and propagated to descendant body step pointers via WorkflowCore's ContextItem chain.
/// </summary>
/// <param name="Item">
/// The current iteration's item. Branched contexts carry <c>null</c> — the item is resolved
/// at binding time from <see cref="ForEachCollectionCache"/> so it is not persisted into
/// every body-step pointer. A non-null value occurs on contexts persisted by older versions
/// (kept deserialisable for in-flight runs, and preferred when present) and on transient
/// contexts built for per-item filter evaluation inside the container body.
/// </param>
/// <param name="Index">The current iteration's zero-based index.</param>
/// <param name="ContainerStepId">The id of the container step (ForEach/While/Parallel) that produced this context.</param>
/// <param name="Parent">The enclosing iteration context, when this iteration is nested inside another container.</param>
internal sealed record ForEachIterationContext(
    object? Item,
    int Index,
    Guid ContainerStepId,
    ForEachIterationContext? Parent = null)
{
    /// <summary>
    /// Gets a stable scope path identifying this iteration (and its ancestors, for nested loops).
    /// Used as the lookup key for iteration-scoped step outputs in <see cref="AutomationWorkflowData.IterationStepOutputs"/>.
    /// </summary>
    public string ScopePath => Parent is null
        ? $"{ContainerStepId:N}:{Index}"
        : $"{Parent.ScopePath}/{ContainerStepId:N}:{Index}";
}
