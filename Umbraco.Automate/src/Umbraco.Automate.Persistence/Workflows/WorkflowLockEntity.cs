namespace Umbraco.Automate.Persistence.Workflows;

/// <summary>
/// EF Core entity backing <c>EFCoreWorkflowLockProvider</c>'s WorkflowCore <c>IDistributedLockProvider</c>
/// implementation. A row is a lease: <see cref="ExpiresUtc"/> in the past means the lock is free (either
/// never held, or released), and bounds how long a crashed holder can block other nodes from stealing it.
/// <para>
/// Not scoped to a workflow instance — <see cref="LockId"/> also holds WorkflowCore's own fixed poll
/// keys (e.g. <c>"poll runnables"</c>), so there is no foreign key to <see cref="WorkflowInstanceEntity"/>.
/// </para>
/// </summary>
internal sealed class WorkflowLockEntity
{
    /// <summary>The resource being locked — a WorkflowCore instance id or a fixed poll key.</summary>
    public required string LockId { get; set; }

    /// <summary>Identifies the process holding the lease; only its owner may release or renew it.</summary>
    public Guid OwnerToken { get; set; }

    public DateTime AcquiredUtc { get; set; }

    /// <summary>Past this instant the lease is free and may be (re)acquired by any process.</summary>
    public DateTime ExpiresUtc { get; set; }
}
