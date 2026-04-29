using Umbraco.Automate.Core.Conditions;

namespace Umbraco.Automate.Core.Execution.ControlFlow;

/// <summary>
/// Describes one outgoing edge from a container step (ForEach, While, Parallel) into
/// a body branch entry, together with any filter that gates the edge.
/// </summary>
/// <remarks>
/// Container outgoing edges can carry <see cref="ConditionSet"/> filters that the user
/// configures in the UI (e.g. "only iterate items where loop.item.x is not empty").
/// WorkflowCore's outcome wiring lives at the step level and runs without iteration
/// context, so these filters cannot be encoded as plain <see cref="WorkflowCore.Models.ValueOutcome"/>
/// values; the container step body evaluates them itself, with iteration context where
/// available, and decides whether to branch.
/// </remarks>
/// <param name="TargetStepId">The body branch entry step ID.</param>
/// <param name="Filter">Optional filter — if all filters across outgoing edges fail, the container suppresses the branch.</param>
internal sealed record ContainerBranchEdge(Guid TargetStepId, ConditionSet? Filter);
