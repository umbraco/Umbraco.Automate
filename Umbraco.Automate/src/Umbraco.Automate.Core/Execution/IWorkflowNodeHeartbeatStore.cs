namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// Storage for the per-node liveness signal <see cref="WorkflowLockProvider"/> writes while its node
/// consumes workflow work. Startup recovery reads it to tell a run another node will carry on with
/// from one this node's previous process abandoned: a workflow lock is only held during an execution
/// pass, so a run that is queued, between steps or waiting to retry has no lock, but the node that
/// will run its next pass is still heartbeating.
/// <para>
/// Lives in Core for the same reason as <see cref="IWorkflowLockStore"/>; the EF Core implementation
/// lives in Persistence.
/// </para>
/// </summary>
internal interface IWorkflowNodeHeartbeatStore
{
    /// <summary>
    /// Records a heartbeat for <paramref name="nodeId"/>: creates its row on the first beat, and on
    /// every later one advances its beat counter so a reader can see it change without comparing
    /// clocks across nodes.
    /// </summary>
    Task BeatAsync(Guid nodeId, DateTime nowUtc, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the heartbeat for <paramref name="nodeId"/>, so a node that stops (or stops consuming
    /// workflow work) is not waited on by the next node to start.
    /// </summary>
    Task RemoveAsync(Guid nodeId, CancellationToken cancellationToken);
}
