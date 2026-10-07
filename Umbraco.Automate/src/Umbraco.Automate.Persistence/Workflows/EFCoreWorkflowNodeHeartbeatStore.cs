using Microsoft.EntityFrameworkCore;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Persistence.Scoping;

namespace Umbraco.Automate.Persistence.Workflows;

/// <summary>
/// EF Core implementation of <see cref="IWorkflowNodeHeartbeatStore"/>.
/// </summary>
internal sealed class EFCoreWorkflowNodeHeartbeatStore : IWorkflowNodeHeartbeatStore
{
    private readonly IDetachedDbContextFactory<UmbracoAutomateDbContext> _dbContextFactory;

    /// <param name="dbContextFactory">
    /// The detached factory, as for <see cref="EFCoreWorkflowLockStore"/>: a heartbeat written inside a
    /// caller's transaction would be invisible to other nodes until that caller commits.
    /// </param>
    public EFCoreWorkflowNodeHeartbeatStore(IDetachedDbContextFactory<UmbracoAutomateDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task BeatAsync(Guid nodeId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var updated = await db.WorkflowNodeHeartbeats
            .Where(h => h.NodeId == nodeId)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(h => h.Beat, h => h.Beat + 1)
                    .SetProperty(h => h.HeartbeatUtc, nowUtc),
                cancellationToken);

        if (updated > 0)
        {
            return;
        }

        // Only this process writes its own row, so there is no insert race to guard against.
        db.WorkflowNodeHeartbeats.Add(new WorkflowNodeHeartbeatEntity
        {
            NodeId = nodeId,
            Beat = 1,
            HeartbeatUtc = nowUtc,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(Guid nodeId, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        await db.WorkflowNodeHeartbeats
            .Where(h => h.NodeId == nodeId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task RemoveStaleAsync(DateTime staleBeforeUtc, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        await db.WorkflowNodeHeartbeats
            .Where(h => h.HeartbeatUtc < staleBeforeUtc)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
