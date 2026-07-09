using Microsoft.EntityFrameworkCore;
using Umbraco.Automate.Core.Execution;

namespace Umbraco.Automate.Persistence.Workflows;

/// <summary>
/// EF Core implementation of <see cref="IWorkflowLockStore"/>, backing
/// <see cref="WorkflowLockProvider"/>'s WorkflowCore <c>IDistributedLockProvider</c>.
/// <para>
/// A <see cref="WorkflowLockEntity"/> row's <c>ExpiresUtc</c> in the past means the lease is free —
/// this covers both a never-held id and one released via <see cref="ReleaseAsync"/>, which marks
/// free rather than deleting so re-acquiring a previously-seen id is a single round trip instead of
/// an insert-after-delete cycle.
/// </para>
/// </summary>
internal sealed class EFCoreWorkflowLockStore : IWorkflowLockStore
{
    private readonly IDbContextFactory<UmbracoAutomateDbContext> _dbContextFactory;

    public EFCoreWorkflowLockStore(IDbContextFactory<UmbracoAutomateDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<bool> TryAcquireAsync(
        string lockId, Guid ownerToken, DateTime nowUtc, DateTime expiresUtc, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Steady-state path: steal a row whose lease has lapsed (never held, or released).
        var stolen = await db.WorkflowLocks
            .Where(l => l.LockId == lockId && l.ExpiresUtc < nowUtc)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(l => l.OwnerToken, ownerToken)
                    .SetProperty(l => l.AcquiredUtc, nowUtc)
                    .SetProperty(l => l.ExpiresUtc, expiresUtc),
                cancellationToken);

        if (stolen > 0)
        {
            return true;
        }

        // No row affected: either the id is live (held by someone else) or has never been seen
        // before. Only the latter can be resolved by inserting — the PK guards a race between two
        // processes both attempting this for the first time.
        db.WorkflowLocks.Add(new WorkflowLockEntity
        {
            LockId = lockId,
            OwnerToken = ownerToken,
            AcquiredUtc = nowUtc,
            ExpiresUtc = expiresUtc,
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Row already exists and is live — someone else holds the lock.
            return false;
        }

        return true;
    }

    public async Task ReleaseAsync(string lockId, Guid ownerToken, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        await db.WorkflowLocks
            .Where(l => l.LockId == lockId && l.OwnerToken == ownerToken)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.ExpiresUtc, DateTime.MinValue), cancellationToken);
    }

    public async Task RenewAsync(
        IReadOnlyCollection<string> lockIds, Guid ownerToken, DateTime expiresUtc, CancellationToken cancellationToken)
    {
        if (lockIds.Count == 0)
        {
            return;
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        await db.WorkflowLocks
            .Where(l => l.OwnerToken == ownerToken && lockIds.Contains(l.LockId))
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.ExpiresUtc, expiresUtc), cancellationToken);
    }
}
