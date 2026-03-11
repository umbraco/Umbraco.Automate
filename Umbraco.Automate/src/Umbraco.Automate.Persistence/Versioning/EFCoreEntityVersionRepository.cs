using Microsoft.EntityFrameworkCore;
using Umbraco.Automate.Core.Versioning;
using Umbraco.Cms.Persistence.EFCore.Scoping;

namespace Umbraco.Automate.Persistence.Versioning;

/// <summary>
/// EF Core implementation of <see cref="IEntityVersionRepository"/>.
/// </summary>
internal sealed class EFCoreEntityVersionRepository : IEntityVersionRepository
{
    private readonly IEFCoreScopeProvider<UmbracoAutomateDbContext> _scopeProvider;

    public EFCoreEntityVersionRepository(IEFCoreScopeProvider<UmbracoAutomateDbContext> scopeProvider)
    {
        _scopeProvider = scopeProvider;
    }

    public async Task<IEnumerable<EntityVersion>> GetVersionHistoryAsync(
        Guid entityId,
        string entityType,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        var entities = await scope.ExecuteWithContextAsync(async db =>
            await db.EntityVersions
                .Where(v => v.EntityId == entityId && v.EntityType == entityType)
                .OrderByDescending(v => v.Version)
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken));

        scope.Complete();
        return entities.Select(MapToDomain);
    }

    public async Task<int> GetVersionCountByEntityAsync(
        Guid entityId,
        string entityType,
        CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        var count = await scope.ExecuteWithContextAsync(async db =>
            await db.EntityVersions
                .CountAsync(v => v.EntityId == entityId && v.EntityType == entityType, cancellationToken));

        scope.Complete();
        return count;
    }

    public async Task<EntityVersion?> GetVersionAsync(
        Guid entityId,
        string entityType,
        int version,
        CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        var entity = await scope.ExecuteWithContextAsync(async db =>
            await db.EntityVersions
                .FirstOrDefaultAsync(v =>
                    v.EntityId == entityId &&
                    v.EntityType == entityType &&
                    v.Version == version,
                    cancellationToken));

        scope.Complete();
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
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        await scope.ExecuteWithContextAsync<UmbracoAutomateDbContext>(async db =>
        {
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
        });

        scope.Complete();
    }

    public async Task DeleteVersionsAsync(
        Guid entityId,
        string entityType,
        CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        await scope.ExecuteWithContextAsync<UmbracoAutomateDbContext>(async db =>
        {
            await db.EntityVersions
                .Where(v => v.EntityId == entityId && v.EntityType == entityType)
                .ExecuteDeleteAsync(cancellationToken);
        });

        scope.Complete();
    }

    public async Task<int> DeleteVersionsOlderThanAsync(
        DateTime threshold,
        IReadOnlyCollection<ProtectedVersion> protectedVersions,
        CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        var deleted = await scope.ExecuteWithContextAsync(async db =>
        {
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
        });

        scope.Complete();
        return deleted;
    }

    public async Task<int> DeleteExcessVersionsAsync(
        int maxVersionsPerEntity,
        IReadOnlyCollection<ProtectedVersion> protectedVersions,
        CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        // Get all entity/type combinations that exceed the limit.
        var totalDeleted = await scope.ExecuteWithContextAsync(async db =>
        {
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
        });

        scope.Complete();
        return totalDeleted;
    }

    public async Task<int> GetVersionCountAsync(CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        var count = await scope.ExecuteWithContextAsync(async db =>
            await db.EntityVersions.CountAsync(cancellationToken));

        scope.Complete();
        return count;
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
