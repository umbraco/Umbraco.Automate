using Microsoft.EntityFrameworkCore;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Cms.Persistence.EFCore.Scoping;

namespace Umbraco.Automate.Persistence.Workspaces;

/// <summary>
/// EF Core implementation of <see cref="IWorkspaceGroupRepository"/>.
/// </summary>
internal sealed class EFCoreWorkspaceGroupRepository : IWorkspaceGroupRepository
{
    private readonly IEFCoreScopeProvider<UmbracoAutomateDbContext> _scopeProvider;
    private readonly WorkspaceGroupFactory _factory;

    public EFCoreWorkspaceGroupRepository(
        IEFCoreScopeProvider<UmbracoAutomateDbContext> scopeProvider,
        WorkspaceGroupFactory factory)
    {
        _scopeProvider = scopeProvider;
        _factory = factory;
    }

    public async Task<WorkspaceGroup?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        WorkspaceGroupEntity? entity = await scope.ExecuteWithContextAsync(async db =>
            await db.WorkspaceGroups.FirstOrDefaultAsync(g => g.Id == id, cancellationToken));

        scope.Complete();
        return entity is null ? null : _factory.BuildDomain(entity);
    }

    public async Task<IEnumerable<WorkspaceGroup>> GetByWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        var entities = await scope.ExecuteWithContextAsync(async db =>
            await db.WorkspaceGroups
                .Where(g => g.WorkspaceId == workspaceId)
                .OrderBy(g => g.Name)
                .ToListAsync(cancellationToken));

        scope.Complete();
        return entities.Select(_factory.BuildDomain);
    }

    public async Task<WorkspaceGroup> SaveAsync(WorkspaceGroup group, CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        await scope.ExecuteWithContextAsync(async db =>
        {
            WorkspaceGroupEntity? existing = await db.WorkspaceGroups.FindAsync([group.Id], cancellationToken);

            if (existing is null)
            {
                WorkspaceGroupEntity newEntity = _factory.BuildEntity(group);
                db.WorkspaceGroups.Add(newEntity);
            }
            else
            {
                _factory.UpdateEntity(existing, group);
            }

            await db.SaveChangesAsync(cancellationToken);
            return true;
        });

        scope.Complete();
        return group;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        var deleted = await scope.ExecuteWithContextAsync(async db =>
        {
            WorkspaceGroupEntity? entity = await db.WorkspaceGroups.FindAsync([id], cancellationToken);
            if (entity is null)
            {
                return false;
            }

            db.WorkspaceGroups.Remove(entity);
            await db.SaveChangesAsync(cancellationToken);
            return true;
        });

        scope.Complete();
        return deleted;
    }

    public async Task<IEnumerable<Guid>> GetChildIdsAsync(Guid parentId, CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        var ids = await scope.ExecuteWithContextAsync(async db =>
            await db.WorkspaceGroups
                .Where(g => g.ParentId == parentId)
                .Select(g => g.Id)
                .ToListAsync(cancellationToken));

        scope.Complete();
        return ids;
    }

    public async Task<bool> NameExistsAsync(Guid workspaceId, Guid? parentId, string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        var exists = await scope.ExecuteWithContextAsync(async db =>
        {
            var query = db.WorkspaceGroups
                .Where(g => g.WorkspaceId == workspaceId && g.ParentId == parentId && g.Name == name);

            if (excludeId.HasValue)
            {
                query = query.Where(g => g.Id != excludeId.Value);
            }

            return await query.AnyAsync(cancellationToken);
        });

        scope.Complete();
        return exists;
    }

    public async Task<int> DeleteByWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        var count = await scope.ExecuteWithContextAsync(async db =>
        {
            var entities = await db.WorkspaceGroups
                .Where(g => g.WorkspaceId == workspaceId)
                .ToListAsync(cancellationToken);

            db.WorkspaceGroups.RemoveRange(entities);
            await db.SaveChangesAsync(cancellationToken);
            return entities.Count;
        });

        scope.Complete();
        return count;
    }
}
