using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Persistence.Scoping;

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
    private readonly IDetachedDbContextFactory<UmbracoAutomateDbContext> _dbContextFactory;

    /// <param name="dbContextFactory">
    /// Deliberately the detached factory: a lease taken inside a caller's transaction would be
    /// invisible to every other holder until that caller commits, and would vanish on rollback while
    /// this process still believed it held the lock.
    /// </param>
    public EFCoreWorkflowLockStore(IDetachedDbContextFactory<UmbracoAutomateDbContext> dbContextFactory)
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
        // before. Only the latter can be resolved by inserting. Two processes can reach this point
        // for the same never-seen id at once, so the insert is conditional on the row still being
        // absent rather than a plain INSERT relying on the PK to reject the loser — an EF Core
        // SaveChangesAsync failure logs at Error before the caller ever sees the exception, which
        // would turn this ordinary, expected race into a false-alarm error log on every occurrence.
        var sql = BuildConditionalInsertSql(db);
        var inserted = await db.Database.ExecuteSqlRawAsync(
            sql, [lockId, nowUtc, expiresUtc, ownerToken], cancellationToken);

        return inserted > 0;
    }

    /// <summary>
    /// Builds the conditional insert against <see cref="WorkflowLockEntity"/>'s table/column names as
    /// EF's model currently maps them (rather than hard-coded literals), so a future rename of the
    /// entity or its mapping cannot silently desync this raw SQL from the schema it targets. Only
    /// identifiers are spliced into the SQL text; the four values are passed as provider parameters
    /// via the numbered placeholders that <see cref="DatabaseFacade.ExecuteSqlRawAsync(string, object[], CancellationToken)"/> substitutes.
    /// </summary>
    private static string BuildConditionalInsertSql(UmbracoAutomateDbContext db)
    {
        var entityType = db.Model.FindEntityType(typeof(WorkflowLockEntity))!;
        var storeObject = StoreObjectIdentifier.Create(entityType, StoreObjectType.Table)!.Value;
        var sqlHelper = db.GetService<ISqlGenerationHelper>();

        string Column(string propertyName) =>
            sqlHelper.DelimitIdentifier(entityType.FindProperty(propertyName)!.GetColumnName(storeObject)!);

        var table = sqlHelper.DelimitIdentifier(entityType.GetTableName()!, entityType.GetSchema());
        var lockIdColumn = Column(nameof(WorkflowLockEntity.LockId));
        var acquiredColumn = Column(nameof(WorkflowLockEntity.AcquiredUtc));
        var expiresColumn = Column(nameof(WorkflowLockEntity.ExpiresUtc));
        var ownerColumn = Column(nameof(WorkflowLockEntity.OwnerToken));

        return $$"""
            INSERT INTO {{table}} ({{lockIdColumn}}, {{acquiredColumn}}, {{expiresColumn}}, {{ownerColumn}})
            SELECT {0}, {1}, {2}, {3}
            WHERE NOT EXISTS (SELECT 1 FROM {{table}} WHERE {{lockIdColumn}} = {0})
            """;
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
