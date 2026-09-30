using Microsoft.EntityFrameworkCore;
using Umbraco.Automate.Core.Persistence.Scoping;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace Umbraco.Automate.Persistence.Workflows;

/// <summary>
/// EF Core-backed <see cref="IWorkflowPurger"/> for WorkflowCore.
/// WorkflowCore's own purger ships with its EF persistence provider, which we replace
/// (see <see cref="EFCoreWorkflowPersistenceProvider"/>), so the engine tables need one of our own.
/// </summary>
internal sealed class EFCoreWorkflowPurger : IWorkflowPurger
{
    private readonly IDetachedDbContextFactory<UmbracoAutomateDbContext> _dbContextFactory;

    public EFCoreWorkflowPurger(IDetachedDbContextFactory<UmbracoAutomateDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    /// <summary>
    /// Deletes finished workflow instances (and their execution pointers and event subscriptions) that completed before <paramref name="olderThan"/>, plus processed events older than it.
    /// Only <see cref="WorkflowStatus.Complete"/> and <see cref="WorkflowStatus.Terminated"/> are
    /// purged; instances the engine may still pick up (runnable or suspended) are never touched.
    /// </summary>
    public async Task PurgeWorkflows(WorkflowStatus status, DateTime olderThan, CancellationToken cancellationToken = default)
    {
        if (status is not (WorkflowStatus.Complete or WorkflowStatus.Terminated))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status), status, "Only complete or terminated workflows can be purged.");
        }

        var statusValue = (int)status;

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var instanceIds = await db.WorkflowInstances
            .Where(w => w.Status == statusValue && w.CompleteTime != null && w.CompleteTime < olderThan)
            .Select(w => w.Id)
            .ToListAsync(cancellationToken);

        foreach (var batch in instanceIds.Chunk(500))
        {
            // Explicit deletes rather than relying on the pointer cascade: subscriptions reference
            // the instance by id only, with no foreign key. Leases expire on their own, so locks are left alone.
            await db.WorkflowExecutionPointers
                .Where(p => batch.Contains(p.WorkflowInstanceId))
                .ExecuteDeleteAsync(cancellationToken);

            await db.EventSubscriptions
                .Where(s => batch.Contains(s.WorkflowId))
                .ExecuteDeleteAsync(cancellationToken);

            await db.WorkflowInstances
                .Where(w => batch.Contains(w.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }

        // Events are not tied to an instance; once processed they only accumulate.
        await db.Events
            .Where(e => e.IsProcessed && e.EventTime < olderThan)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
