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
        var forwardAdj = new Dictionary<Guid, List<(Guid Target, string? Outcome)>>();
        var reverseAdj = new Dictionary<Guid, List<Guid>>();

        foreach (var conn in connections)
        {
            if (!forwardAdj.TryGetValue(conn.SourceStepId, out var fwd))
            {
                fwd = [];
                forwardAdj[conn.SourceStepId] = fwd;
            }
            fwd.Add((conn.TargetStepId, conn.Outcome));

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
        Dictionary<Guid, List<(Guid Target, string? Outcome)>> forwardAdj,
        IReadOnlySet<Guid> containerStepIds)
    {
        if (!forwardAdj.TryGetValue(containerId, out var outgoing) || outgoing.Count == 0)
        {
            return new ContainerScope(new HashSet<Guid>(), null);
        }

        // Collect branch start steps — immediate successors of the container.
        var branchStarts = outgoing.Select(o => o.Target).ToList();

        // For each branch, walk forward collecting all reachable steps.
        var branchReachable = new List<HashSet<Guid>>();
        foreach (var start in branchStarts)
        {
            var reachable = new HashSet<Guid>();
            WalkForward(start, forwardAdj, containerStepIds, reachable);
            branchReachable.Add(reachable);
        }

        // Find convergence point: the first step reachable from ALL branches (but not the container itself).
        Guid? convergenceId = null;
        if (branchReachable.Count > 1)
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

        // Children = all steps reachable from branch starts, minus the convergence point and beyond.
        var children = new HashSet<Guid>();
        foreach (var reachable in branchReachable)
        {
            children.UnionWith(reachable);
        }

        if (convergenceId.HasValue)
        {
            // Remove the convergence point and everything reachable from it.
            children.Remove(convergenceId.Value);
            var beyondConvergence = new HashSet<Guid>();
            WalkForward(convergenceId.Value, forwardAdj, containerStepIds, beyondConvergence);
            children.ExceptWith(beyondConvergence);
        }

        return new ContainerScope(children, convergenceId);
    }

    private static void WalkForward(
        Guid start,
        Dictionary<Guid, List<(Guid Target, string? Outcome)>> forwardAdj,
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

            foreach (var (target, _) in outgoing)
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
        Dictionary<Guid, List<(Guid Target, string? Outcome)>> forwardAdj)
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

            foreach (var (target, _) in outgoing)
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
}

/// <summary>
/// Represents the scope of a container control flow step — which steps are its children
/// and where the execution converges back to the main flow.
/// </summary>
/// <param name="ChildStepIds">The set of step IDs that are children of this container.</param>
/// <param name="ConvergenceStepId">The step ID where branches converge (the container's successor), or null if terminal.</param>
internal sealed record ContainerScope(IReadOnlySet<Guid> ChildStepIds, Guid? ConvergenceStepId);
