using Microsoft.EntityFrameworkCore;
using Umbraco.Automate.Core;
using Umbraco.Automate.Core.Workspaces;

namespace Umbraco.Automate.Persistence.Workspaces;

/// <summary>
/// EF Core implementation of <see cref="IWorkspaceRepository"/>.
/// </summary>
internal sealed class EFCoreWorkspaceRepository : IWorkspaceRepository
{
    private readonly IDbContextFactory<UmbracoAutomateDbContext> _dbContextFactory;

    public EFCoreWorkspaceRepository(IDbContextFactory<UmbracoAutomateDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<Workspace?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        WorkspaceEntity? entity = await db.Workspaces
            .Include(w => w.UserGroups)
            .Include(w => w.AllowedConnections)
            .AsSplitQuery()
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

        return entity is null ? null : WorkspaceFactory.BuildDomain(entity);
    }

    public async Task<Workspace?> GetByAliasAsync(string alias, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        WorkspaceEntity? entity = await db.Workspaces
            .Include(w => w.UserGroups)
            .Include(w => w.AllowedConnections)
            .AsSplitQuery()
            .FirstOrDefaultAsync(w => w.Alias == alias, cancellationToken);

        return entity is null ? null : WorkspaceFactory.BuildDomain(entity);
    }

    public async Task<IEnumerable<Workspace>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entities = await db.Workspaces
            .Include(w => w.UserGroups)
            .Include(w => w.AllowedConnections)
            .AsSplitQuery()
            .OrderBy(w => w.Name)
            .ToListAsync(cancellationToken);

        return entities.Select(WorkspaceFactory.BuildDomain);
    }

    public async Task<(IEnumerable<Workspace> Items, int Total)> GetPagedAsync(
        string? filter = null,
        IReadOnlyCollection<Guid>? userGroupKeys = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        IQueryable<WorkspaceEntity> query = db.Workspaces
            .Include(w => w.UserGroups)
            .Include(w => w.AllowedConnections)
            .AsSplitQuery();

        if (userGroupKeys is { Count: > 0 })
        {
            query = query.Where(w => w.UserGroups.Any(ug => userGroupKeys.Contains(ug.UserGroupId)));
        }

        if (!string.IsNullOrWhiteSpace(filter))
        {
            // Lower-case both sides so matching is case-insensitive across providers. On SQLite,
            // string.Contains translates to a case-sensitive instr(); comparing lower() forms avoids that.
            var loweredFilter = filter.ToLower();
            query = query.Where(w => w.Name.ToLower().Contains(loweredFilter) || w.Alias.ToLower().Contains(loweredFilter));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(w => w.Name)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return (items.Select(WorkspaceFactory.BuildDomain), total);
    }

    public async Task<Workspace> SaveAsync(Workspace workspace, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        WorkspaceEntity? existing = await db.Workspaces
            .Include(w => w.UserGroups)
            .Include(w => w.AllowedConnections)
            .AsSplitQuery()
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
            // Set EF Core's original value to the client's version so the WHERE clause
            // detects races between the controller read and this repository save.
            db.Entry(existing).Property(e => e.Version).OriginalValue = workspace.Version;

            workspace.Version = existing.Version + 1;
            workspace.DateModified = DateTime.UtcNow;
            workspace.ModifiedByUserId = userId;

            // Remove old join table rows, EF will add new ones via UpdateEntity
            db.Set<WorkspaceUserGroupEntity>().RemoveRange(existing.UserGroups);
            db.Set<WorkspaceConnectionEntity>().RemoveRange(existing.AllowedConnections);

            WorkspaceFactory.UpdateEntity(existing, workspace);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException(nameof(Workspace), workspace.Id);
        }

        return workspace;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        WorkspaceEntity? entity = await db.Workspaces.FindAsync([id], cancellationToken);
        if (entity is null)
        {
            return false;
        }

        db.Workspaces.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Workspaces.AnyAsync(w => w.Id == id, cancellationToken);
    }

    public async Task<IReadOnlySet<Guid>> GetIdsByUserGroupKeysAsync(
        IEnumerable<Guid> userGroupKeys,
        CancellationToken cancellationToken = default)
    {
        var keys = userGroupKeys.ToList();
        if (keys.Count == 0)
        {
            return new HashSet<Guid>();
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var ids = await db.Set<WorkspaceUserGroupEntity>()
            .Where(ug => keys.Contains(ug.UserGroupId))
            .Select(ug => ug.WorkspaceId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return ids.ToHashSet();
    }
}
