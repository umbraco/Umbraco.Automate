using Microsoft.EntityFrameworkCore;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Cms.Persistence.EFCore.Scoping;

namespace Umbraco.Automate.Persistence.Workspaces;

/// <summary>
/// EF Core implementation of <see cref="IWorkspaceRepository"/>.
/// </summary>
internal sealed class EFCoreWorkspaceRepository : IWorkspaceRepository
{
    private readonly IEFCoreScopeProvider<UmbracoAutomateDbContext> _scopeProvider;

    public EFCoreWorkspaceRepository(IEFCoreScopeProvider<UmbracoAutomateDbContext> scopeProvider)
    {
        _scopeProvider = scopeProvider;
    }

    public async Task<Workspace?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        WorkspaceEntity? entity = await scope.ExecuteWithContextAsync(async db =>
            await db.Workspaces
                .Include(w => w.UserGroups)
                .Include(w => w.Connections)
                .FirstOrDefaultAsync(w => w.Id == id, cancellationToken));

        scope.Complete();
        return entity is null ? null : WorkspaceFactory.BuildDomain(entity);
    }

    public async Task<Workspace?> GetByAliasAsync(string alias, CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        WorkspaceEntity? entity = await scope.ExecuteWithContextAsync(async db =>
            await db.Workspaces
                .Include(w => w.UserGroups)
                .Include(w => w.Connections)
                .FirstOrDefaultAsync(w => w.Alias == alias, cancellationToken));

        scope.Complete();
        return entity is null ? null : WorkspaceFactory.BuildDomain(entity);
    }

    public async Task<IEnumerable<Workspace>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        var entities = await scope.ExecuteWithContextAsync(async db =>
            await db.Workspaces
                .Include(w => w.UserGroups)
                .Include(w => w.Connections)
                .OrderBy(w => w.Name)
                .ToListAsync(cancellationToken));

        scope.Complete();
        return entities.Select(WorkspaceFactory.BuildDomain);
    }

    public async Task<(IEnumerable<Workspace> Items, int Total)> GetPagedAsync(
        string? filter = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        var result = await scope.ExecuteWithContextAsync(async db =>
        {
            IQueryable<WorkspaceEntity> query = db.Workspaces
                .Include(w => w.UserGroups)
                .Include(w => w.Connections);

            if (!string.IsNullOrWhiteSpace(filter))
            {
                query = query.Where(w => w.Name.Contains(filter) || w.Alias.Contains(filter));
            }

            var total = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderBy(w => w.Name)
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);

            return (items, total);
        });

        scope.Complete();
        return (result.items.Select(WorkspaceFactory.BuildDomain), result.total);
    }

    public async Task<Workspace> SaveAsync(Workspace workspace, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        var savedWorkspace = await scope.ExecuteWithContextAsync(async db =>
        {
            WorkspaceEntity? existing = await db.Workspaces
                .Include(w => w.UserGroups)
                .Include(w => w.Connections)
                .FirstOrDefaultAsync(w => w.Id == workspace.Id, cancellationToken);

            if (existing is null)
            {
                workspace.Version = 1;
                workspace.DateModified = DateTime.UtcNow;
                workspace.CreatedByUserId = userId;
                workspace.ModifiedByUserId = userId;

                WorkspaceEntity newEntity = WorkspaceFactory.BuildEntity(workspace);
                db.Workspaces.Add(newEntity);
            }
            else
            {
                workspace.Version = existing.Version + 1;
                workspace.DateModified = DateTime.UtcNow;
                workspace.ModifiedByUserId = userId;

                // Remove old join table rows, EF will add new ones via UpdateEntity
                db.Set<WorkspaceUserGroupEntity>().RemoveRange(existing.UserGroups);
                db.Set<WorkspaceConnectionEntity>().RemoveRange(existing.Connections);

                WorkspaceFactory.UpdateEntity(existing, workspace);
            }

            await db.SaveChangesAsync(cancellationToken);
            return workspace;
        });

        scope.Complete();
        return savedWorkspace;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        var deleted = await scope.ExecuteWithContextAsync(async db =>
        {
            WorkspaceEntity? entity = await db.Workspaces.FindAsync([id], cancellationToken);
            if (entity is null)
            {
                return false;
            }

            db.Workspaces.Remove(entity);
            await db.SaveChangesAsync(cancellationToken);
            return true;
        });

        scope.Complete();
        return deleted;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        var exists = await scope.ExecuteWithContextAsync(async db =>
            await db.Workspaces.AnyAsync(w => w.Id == id, cancellationToken));

        scope.Complete();
        return exists;
    }
}
