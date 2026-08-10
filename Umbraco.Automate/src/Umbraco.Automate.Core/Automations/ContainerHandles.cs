namespace Umbraco.Automate.Core.Automations;

/// <summary>
/// Well-known <see cref="StepConnection.SourceHandle"/> values for container control flow steps
/// (While, ForEach, Parallel). A container renders two source handles on the canvas: the body
/// handle, whose targets run inside the loop, and the done handle, whose target runs once after
/// the container completes.
/// </summary>
/// <remarks>
/// These values are mirrored client-side in <c>canvas/utils/model-to-flow.ts</c> as the React Flow
/// handle ids, and must stay in step with it.
/// <para>
/// Connections saved before these handles existed carry a null <see cref="StepConnection.SourceHandle"/>.
/// They are treated as body edges, and the post-loop step continues to be inferred from graph shape
/// (see <see cref="Execution.GraphAnalyzer"/>) so existing automations keep their behaviour.
/// </para>
/// </remarks>
public static class ContainerHandles
{
    /// <summary>
    /// Handle whose targets form the container's body — the steps that run per iteration
    /// (While, ForEach) or per branch (Parallel).
    /// </summary>
    public const string Body = "body";

    /// <summary>
    /// Handle whose target runs once, after the container has finished every iteration or branch.
    /// </summary>
    public const string Done = "done";

    /// <summary>
    /// Returns true when <paramref name="sourceHandle"/> is the done handle.
    /// </summary>
    public static bool IsDone(string? sourceHandle)
        => string.Equals(sourceHandle, Done, StringComparison.OrdinalIgnoreCase);
}
