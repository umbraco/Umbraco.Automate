namespace Umbraco.Automate.Persistence.Workflows;

/// <summary>
/// EF Core entity backing <see cref="EFCoreWorkflowNodeHeartbeatStore"/>: one row per running process
/// that is consuming workflow work, written every <c>WorkflowLock:RenewalInterval</c>.
/// </summary>
internal sealed class WorkflowNodeHeartbeatEntity
{
    /// <summary>
    /// Identifies the process — the same owner token its <see cref="WorkflowLockEntity"/> leases carry,
    /// so recovery can tell a lease left by a process it has seen stop from one held by a live node.
    /// </summary>
    public Guid NodeId { get; set; }

    /// <summary>
    /// Incremented on every heartbeat. Readers decide liveness by watching this change rather than by
    /// comparing <see cref="HeartbeatUtc"/> with their own clock, so clock skew between nodes cannot
    /// make a live node look dead.
    /// </summary>
    public long Beat { get; set; }

    /// <summary>
    /// The writer's clock at the last heartbeat. Only used to discard rows far older than any
    /// plausible skew without waiting on them.
    /// </summary>
    public DateTime HeartbeatUtc { get; set; }
}
