using Umbraco.Automate.Core.Automations;

namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// Analyzes the flat step connection graph to identify container children and convergence points
/// for control flow steps that own a body of steps (ForEach, While, Parallel).
/// </summary>
internal static class GraphAnalyzer
{
    /// <summary>
    /// Analyzes the step graph and returns scope information for container control flow steps.
    /// </summary>
    /// <param name="connections">The connections between steps.</param>
    /// <param name="containerStepIds">The set of step IDs that are container control flow steps.</param>
    /// <returns>A dictionary mapping container step IDs to their scope analysis results.</returns>
    public static Dictionary<Guid, ContainerScope> Analyze(
        IList<StepConnection> connections,
        IReadOnlySet<Guid> containerStepIds)
    {
        if (containerStepIds.Count == 0)
        {
            return [];
        }

        // Build forward adjacency from connections.
        var forwardAdj = new Dictionary<Guid, List<Edge>>();
        var reverseAdj = new Dictionary<Guid, List<Guid>>();

        foreach (var conn in connections)
        {
            if (!forwardAdj.TryGetValue(conn.SourceStepId, out var fwd))
            {
                fwd = [];
                forwardAdj[conn.SourceStepId] = fwd;
            }
            fwd.Add(new Edge(conn.TargetStepId, conn.Outcome, conn.SourceHandle));

            if (!reverseAdj.TryGetValue(conn.TargetStepId, out var rev))
            {
                rev = [];
                reverseAdj[conn.TargetStepId] = rev;
            }
            rev.Add(conn.SourceStepId);
        }

        var result = new Dictionary<Guid, ContainerScope>();

        foreach (var containerId in containerStepIds)
        {
            var scope = AnalyzeContainer(containerId, forwardAdj, containerStepIds);
            result[containerId] = scope;
        }

        return result;
    }

    private static ContainerScope AnalyzeContainer(
        Guid containerId,
        Dictionary<Guid, List<Edge>> forwardAdj,
        IReadOnlySet<Guid> containerStepIds)
    {
        if (!forwardAdj.TryGetValue(containerId, out var outgoing) || outgoing.Count == 0)
        {
            return new ContainerScope(new HashSet<Guid>(), new HashSet<Guid>(), null);
        }

        // An edge leaving the done handle names the post-loop step outright, so there is nothing
        // to infer. Everything else leaving the container is a body branch — including edges with
        // no handle at all, which is what connections saved before the handles existed look like.
        Guid? doneTarget = null;
        var bodyOutgoing = new List<Edge>(outgoing.Count);
        foreach (var edge in outgoing)
        {
            if (ContainerHandles.IsDone(edge.SourceHandle))
            {
                // A container has one done handle, so extra done edges are a malformed graph.
                // Take the first and let the rest fall through as body edges rather than throwing
                // at compile time — a broken canvas should not stop the whole automation loading.
                doneTarget ??= edge.Target;
                continue;
            }

            bodyOutgoing.Add(edge);
        }

        // Collect branch entry steps — immediate successors of the container via the body handle.
        // These are the only steps that go into WorkflowStep.Children. Steps further
        // down the body chain are reached via outcome routing, not by being spawned
        // as additional children when the container branches.
        var branchEntries = new HashSet<Guid>(bodyOutgoing.Select(o => o.Target));

        // For each branch entry, walk forward collecting all reachable steps. This
        // gives us the transitive body membership, used for convergence detection.
        var branchReachable = new List<HashSet<Guid>>();
        foreach (var start in branchEntries)
        {
            var reachable = new HashSet<Guid>();
            WalkForward(start, forwardAdj, containerStepIds, reachable);
            branchReachable.Add(reachable);
        }

        var convergenceId = doneTarget;

        // Legacy fallback: with no done edge, the post-loop step is whatever the branches converge
        // on. This is the only way to express "after the loop" before the handles existed, so it
        // stays supported for automations saved back then. Remove once those can no longer exist.
        if (!convergenceId.HasValue && branchReachable.Count > 1)
        {
            // Intersection of all branch reachable sets.
            var intersection = new HashSet<Guid>(branchReachable[0]);
            for (var i = 1; i < branchReachable.Count; i++)
            {
                intersection.IntersectWith(branchReachable[i]);
            }

            // The convergence point is the one with shortest path from any branch start.
            // Use BFS from the container to find the first intersection member.
            if (intersection.Count > 0)
            {
                convergenceId = FindNearestByBfs(containerId, intersection, forwardAdj);
            }
        }

        // Body membership = all steps reachable from branch entries, minus the convergence point and beyond.
        var bodyMembers = new HashSet<Guid>();
        foreach (var reachable in branchReachable)
        {
            bodyMembers.UnionWith(reachable);
        }

        if (convergenceId.HasValue)
        {
            // Remove the convergence point and everything reachable from it.
            bodyMembers.Remove(convergenceId.Value);
            var beyondConvergence = new HashSet<Guid>();
            WalkForward(convergenceId.Value, forwardAdj, containerStepIds, beyondConvergence);
            bodyMembers.ExceptWith(beyondConvergence);
        }

        return new ContainerScope(bodyMembers, branchEntries, convergenceId);
    }

    private static void WalkForward(
        Guid start,
        Dictionary<Guid, List<Edge>> forwardAdj,
        IReadOnlySet<Guid> containerStepIds,
        HashSet<Guid> visited)
    {
        var queue = new Queue<Guid>();
        queue.Enqueue(start);
        visited.Add(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (!forwardAdj.TryGetValue(current, out var outgoing))
            {
                continue;
            }

            foreach (var (target, _, _) in outgoing)
            {
                // Don't cross into other container boundaries.
                if (containerStepIds.Contains(target))
                {
                    continue;
                }

                if (visited.Add(target))
                {
                    queue.Enqueue(target);
                }
            }
        }
    }

    private static Guid? FindNearestByBfs(
        Guid start,
        HashSet<Guid> targets,
        Dictionary<Guid, List<Edge>> forwardAdj)
    {
        var visited = new HashSet<Guid> { start };
        var queue = new Queue<Guid>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (!forwardAdj.TryGetValue(current, out var outgoing))
            {
                continue;
            }

            foreach (var (target, _, _) in outgoing)
            {
                if (targets.Contains(target))
                {
                    return target;
                }

                if (visited.Add(target))
                {
                    queue.Enqueue(target);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// One outgoing connection, reduced to what graph analysis needs.
    /// </summary>
    /// <param name="Target">The step the connection points at.</param>
    /// <param name="Outcome">The named outcome that activates the connection, if any.</param>
    /// <param name="SourceHandle">
    /// The canvas handle the connection leaves from. For containers this distinguishes a body edge
    /// from a done edge (see <see cref="ContainerHandles"/>); null on connections saved before the
    /// handles existed.
    /// </param>
    private readonly record struct Edge(Guid Target, string? Outcome, string? SourceHandle);
}

/// <summary>
/// Represents the scope of a container control flow step — which steps are its children
/// and where the execution converges back to the main flow.
/// </summary>
/// <param name="BodyMemberStepIds">All step IDs that belong to this container's body (transitive body membership).</param>
/// <param name="BranchEntryStepIds">
/// Direct successors of the container — the steps that act as branch entry points when the
/// container spawns child execution pointers. Only these are added to <c>WorkflowStep.Children</c>;
/// further siblings inside the body are reached via outcome routing.
/// </param>
/// <param name="ConvergenceStepId">The step ID where branches converge (the container's successor), or null if terminal.</param>
internal sealed record ContainerScope(
    IReadOnlySet<Guid> BodyMemberStepIds,
    IReadOnlySet<Guid> BranchEntryStepIds,
    Guid? ConvergenceStepId);
