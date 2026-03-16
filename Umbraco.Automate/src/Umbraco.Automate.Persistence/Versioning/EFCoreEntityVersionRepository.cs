using Microsoft.EntityFrameworkCore;
using Umbraco.Automate.Core.Versioning;

namespace Umbraco.Automate.Persistence.Versioning;

/// <summary>
/// EF Core implementation of <see cref="IEntityVersionRepository"/>.
/// </summary>
internal sealed class EFCoreEntityVersionRepository : IEntityVersionRepository
{
    private readonly IDbContextFactory<UmbracoAutomateDbContext> _dbContextFactory;

    public EFCoreEntityVersionRepository(IDbContextFactory<UmbracoAutomateDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<(IEnumerable<EntityVersion> Items, int Total)> GetVersionHistoryPagedAsync(
        Guid entityId,
        string entityType,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var query = db.EntityVersions
            .Where(v => v.EntityId == entityId && v.EntityType == entityType);

        var total = await query.CountAsync(cancellationToken);

        var entities = await query
            .OrderByDescending(v => v.Version)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return (entities.Select(MapToDomain), total);
    }

    public async Task<EntityVersion?> GetVersionAsync(
        Guid entityId,
        string entityType,
        int version,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await db.EntityVersions
            .FirstOrDefaultAsync(v =>
                v.EntityId == entityId &&
                v.EntityType == entityType &&
                v.Version == version,
                cancellationToken);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task SaveVersionAsync(
        Guid entityId,
        string entityType,
        int version,
        string snapshot,
        Guid? userId,
        string? changeDescription,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        db.EntityVersions.Add(new EntityVersionEntity
        {
            Id = Guid.NewGuid(),
            EntityId = entityId,
            EntityType = entityType,
            Version = version,
            Snapshot = snapshot,
            DateCreated = DateTime.UtcNow,
            CreatedByUserId = userId,
            ChangeDescription = changeDescription,
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteVersionsAsync(
        Guid entityId,
        string entityType,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        await db.EntityVersions
            .Where(v => v.EntityId == entityId && v.EntityType == entityType)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<int> DeleteVersionsOlderThanAsync(
        DateTime threshold,
        IReadOnlyCollection<ProtectedVersion> protectedVersions,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        if (protectedVersions.Count == 0)
        {
            return await db.EntityVersions
                .Where(v => v.DateCreated < threshold)
                .ExecuteDeleteAsync(cancellationToken);
        }

        // Materialize candidates, filter out protected versions in memory, delete by ID.
        var protectedSet = protectedVersions.ToHashSet();
        var candidates = await db.EntityVersions
            .Where(v => v.DateCreated < threshold)
            .Select(v => new { v.Id, v.EntityId, v.Version })
            .ToListAsync(cancellationToken);

        var idsToDelete = candidates
            .Where(v => !protectedSet.Contains(new ProtectedVersion(v.EntityId, v.Version)))
            .Select(v => v.Id)
            .ToList();

        if (idsToDelete.Count == 0)
        {
            return 0;
        }

        return await db.EntityVersions
            .Where(v => idsToDelete.Contains(v.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<int> DeleteExcessVersionsAsync(
        int maxVersionsPerEntity,
        IReadOnlyCollection<ProtectedVersion> protectedVersions,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Get all entity/type combinations that exceed the limit.
        var groups = await db.EntityVersions
            .GroupBy(v => new { v.EntityId, v.EntityType })
            .Where(g => g.Count() > maxVersionsPerEntity)
            .Select(g => new { g.Key.EntityId, g.Key.EntityType, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var protectedSet = protectedVersions.ToHashSet();

        var deleted = 0;
        foreach (var group in groups)
        {
            var candidates = await db.EntityVersions
                .Where(v => v.EntityId == group.EntityId && v.EntityType == group.EntityType)
                .OrderByDescending(v => v.Version)
                .Skip(maxVersionsPerEntity)
                .ToListAsync(cancellationToken);

            var idsToDelete = candidates
                .Where(v => !protectedSet.Contains(new ProtectedVersion(v.EntityId, v.Version)))
                .Select(v => v.Id)
                .ToList();

            if (idsToDelete.Count > 0)
            {
                deleted += await db.EntityVersions
                    .Where(v => idsToDelete.Contains(v.Id))
                    .ExecuteDeleteAsync(cancellationToken);
            }
        }

        return deleted;
    }

    public async Task<int> GetVersionCountAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.EntityVersions.CountAsync(cancellationToken);
    }

    private static EntityVersion MapToDomain(EntityVersionEntity entity) => new()
    {
        Id = entity.Id,
        EntityId = entity.EntityId,
        EntityType = entity.EntityType,
        Version = entity.Version,
        Snapshot = entity.Snapshot,
        DateCreated = entity.DateCreated,
        CreatedByUserId = entity.CreatedByUserId,
        ChangeDescription = entity.ChangeDescription,
    };
}
