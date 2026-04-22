using Microsoft.EntityFrameworkCore;
using Umbraco.Automate.Core.Workspaces;

namespace Umbraco.Automate.Persistence.Workspaces;

/// <summary>
/// EF Core implementation of <see cref="IWorkspaceGroupRepository"/>.
/// </summary>
internal sealed class EFCoreWorkspaceGroupRepository : IWorkspaceGroupRepository
{
    private readonly IDbContextFactory<UmbracoAutomateDbContext> _dbContextFactory;
    private readonly WorkspaceGroupFactory _factory;

    public EFCoreWorkspaceGroupRepository(
        IDbContextFactory<UmbracoAutomateDbContext> dbContextFactory,
        WorkspaceGroupFactory factory)
    {
        _dbContextFactory = dbContextFactory;
        _factory = factory;
    }

    public async Task<WorkspaceGroup?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        WorkspaceGroupEntity? entity = await db.WorkspaceGroups
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

        return entity is null ? null : _factory.BuildDomain(entity);
    }

    public async Task<IEnumerable<WorkspaceGroup>> GetByWorkspaceAsync(Guid workspaceId, Guid? parentId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entities = await db.WorkspaceGroups
            .Where(g => g.WorkspaceId == workspaceId && g.ParentId == parentId)
            .OrderBy(g => g.Name)
            .ToListAsync(cancellationToken);

        return entities.Select(_factory.BuildDomain);
    }

    public async Task<IEnumerable<WorkspaceGroup>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entities = await db.WorkspaceGroups
            .OrderBy(g => g.WorkspaceId)
            .ThenBy(g => g.Name)
            .ToListAsync(cancellationToken);

        return entities.Select(_factory.BuildDomain);
    }

    public async Task<WorkspaceGroup> SaveAsync(WorkspaceGroup group, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

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
        return group;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        WorkspaceGroupEntity? entity = await db.WorkspaceGroups.FindAsync([id], cancellationToken);
        if (entity is null)
        {
            return false;
        }

        db.WorkspaceGroups.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IEnumerable<Guid>> GetChildIdsAsync(Guid parentId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await db.WorkspaceGroups
            .Where(g => g.ParentId == parentId)
            .Select(g => g.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> NameExistsAsync(Guid workspaceId, Guid? parentId, string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var query = db.WorkspaceGroups
            .Where(g => g.WorkspaceId == workspaceId && g.ParentId == parentId && g.Name == name);

        if (excludeId.HasValue)
        {
            query = query.Where(g => g.Id != excludeId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<int> DeleteByWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entities = await db.WorkspaceGroups
            .Where(g => g.WorkspaceId == workspaceId)
            .ToListAsync(cancellationToken);

        db.WorkspaceGroups.RemoveRange(entities);
        await db.SaveChangesAsync(cancellationToken);
        return entities.Count;
    }
}
