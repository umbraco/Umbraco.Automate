using Microsoft.EntityFrameworkCore;
using Umbraco.Automate.Core.Automations;
using Umbraco.Cms.Persistence.EFCore.Scoping;

namespace Umbraco.Automate.Persistence.Automations;

/// <summary>
/// EF Core implementation of <see cref="IAutomationGroupRepository"/>.
/// </summary>
internal sealed class EFCoreAutomationGroupRepository : IAutomationGroupRepository
{
    private readonly IEFCoreScopeProvider<UmbracoAutomateDbContext> _scopeProvider;
    private readonly AutomationGroupFactory _factory;

    public EFCoreAutomationGroupRepository(
        IEFCoreScopeProvider<UmbracoAutomateDbContext> scopeProvider,
        AutomationGroupFactory factory)
    {
        _scopeProvider = scopeProvider;
        _factory = factory;
    }

    public async Task<AutomationGroup?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        AutomationGroupEntity? entity = await scope.ExecuteWithContextAsync(async db =>
            await db.AutomationGroups.FirstOrDefaultAsync(g => g.Id == id, cancellationToken));

        scope.Complete();
        return entity is null ? null : _factory.BuildDomain(entity);
    }

    public async Task<IEnumerable<AutomationGroup>> GetByWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        var entities = await scope.ExecuteWithContextAsync(async db =>
            await db.AutomationGroups
                .Where(g => g.WorkspaceId == workspaceId)
                .OrderBy(g => g.Name)
                .ToListAsync(cancellationToken));

        scope.Complete();
        return entities.Select(_factory.BuildDomain);
    }

    public async Task<AutomationGroup> SaveAsync(AutomationGroup group, CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        await scope.ExecuteWithContextAsync(async db =>
        {
            AutomationGroupEntity? existing = await db.AutomationGroups.FindAsync([group.Id], cancellationToken);

            if (existing is null)
            {
                AutomationGroupEntity newEntity = _factory.BuildEntity(group);
                db.AutomationGroups.Add(newEntity);
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
            AutomationGroupEntity? entity = await db.AutomationGroups.FindAsync([id], cancellationToken);
            if (entity is null)
            {
                return false;
            }

            db.AutomationGroups.Remove(entity);
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
            await db.AutomationGroups
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
            var query = db.AutomationGroups
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
            var entities = await db.AutomationGroups
                .Where(g => g.WorkspaceId == workspaceId)
                .ToListAsync(cancellationToken);

            db.AutomationGroups.RemoveRange(entities);
            await db.SaveChangesAsync(cancellationToken);
            return entities.Count;
        });

        scope.Complete();
        return count;
    }
}
