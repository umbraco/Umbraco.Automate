namespace Umbraco.Automate.Core.Execution.ControlFlow;

/// <summary>
/// Context data passed to each iteration of a ForEach loop.
/// Used as the branch value in <see cref="WorkflowCore.Models.ExecutionResult.Branch"/>.
/// </summary>
internal sealed record ForEachIterationContext(object? Item, int Index);
