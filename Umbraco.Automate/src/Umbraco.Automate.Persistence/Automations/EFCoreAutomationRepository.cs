using Microsoft.EntityFrameworkCore;
using Umbraco.Automate.Core.Automations;
using Umbraco.Cms.Persistence.EFCore.Scoping;

namespace Umbraco.Automate.Persistence.Automations;

/// <summary>
/// EF Core implementation of <see cref="IAutomationRepository"/>.
/// </summary>
internal sealed class EFCoreAutomationRepository : IAutomationRepository
{
    private readonly IEFCoreScopeProvider<UmbracoAutomateDbContext> _scopeProvider;
    private readonly AutomationFactory _factory;

    public EFCoreAutomationRepository(
        IEFCoreScopeProvider<UmbracoAutomateDbContext> scopeProvider,
        AutomationFactory factory)
    {
        _scopeProvider = scopeProvider;
        _factory = factory;
    }

    public async Task<Automation?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        AutomationEntity? entity = await scope.ExecuteWithContextAsync(async db =>
            await db.Automations.FirstOrDefaultAsync(a => a.Id == id, cancellationToken));

        scope.Complete();
        return entity is null ? null : _factory.BuildDomain(entity);
    }

    public async Task<Automation?> GetByAliasAsync(string alias, CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        AutomationEntity? entity = await scope.ExecuteWithContextAsync(async db =>
            await db.Automations.FirstOrDefaultAsync(
                a => a.Alias == alias, cancellationToken));

        scope.Complete();
        return entity is null ? null : _factory.BuildDomain(entity);
    }

    public async Task<IEnumerable<Automation>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        var entities = await scope.ExecuteWithContextAsync(async db =>
            await db.Automations
                .OrderBy(a => a.Name)
                .ToListAsync(cancellationToken));

        scope.Complete();
        return entities.Select(_factory.BuildDomain);
    }

    public async Task<(IEnumerable<Automation> Items, int Total)> GetPagedAsync(
        string? filter = null,
        IReadOnlySet<Guid>? workspaceIds = null,
        Guid? groupId = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        var result = await scope.ExecuteWithContextAsync(async db =>
        {
            IQueryable<AutomationEntity> query = db.Automations;

            if (workspaceIds is not null)
            {
                query = query.Where(a => workspaceIds.Contains(a.WorkspaceId));
            }

            if (groupId is not null)
            {
                // Guid.Empty means "root level" (no group).
                query = groupId.Value == Guid.Empty
                    ? query.Where(a => a.GroupId == null)
                    : query.Where(a => a.GroupId == groupId.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter))
            {
                query = query.Where(a => a.Name.Contains(filter) || a.Alias.Contains(filter));
            }

            var total = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderBy(a => a.Name)
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);

            return (items, total);
        });

        scope.Complete();
        return (result.items.Select(_factory.BuildDomain), result.total);
    }

    public async Task<Automation> SaveAsync(Automation automation, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        var savedAutomation = await scope.ExecuteWithContextAsync(async db =>
        {
            AutomationEntity? existing = await db.Automations.FindAsync([automation.Id], cancellationToken);

            if (existing is null)
            {
                automation.Version = 1;
                automation.DateModified = DateTime.UtcNow;
                automation.CreatedByUserId = userId;
                automation.ModifiedByUserId = userId;

                AutomationEntity newEntity = _factory.BuildEntity(automation);
                db.Automations.Add(newEntity);
            }
            else
            {
                automation.Version = existing.Version + 1;
                automation.DateModified = DateTime.UtcNow;
                automation.ModifiedByUserId = userId;

                _factory.UpdateEntity(existing, automation);
            }

            await db.SaveChangesAsync(cancellationToken);
            return automation;
        });

        scope.Complete();
        return savedAutomation;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        var deleted = await scope.ExecuteWithContextAsync(async db =>
        {
            AutomationEntity? entity = await db.Automations.FindAsync([id], cancellationToken);
            if (entity is null)
            {
                return false;
            }

            db.Automations.Remove(entity);
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
            await db.Automations.AnyAsync(a => a.Id == id, cancellationToken));

        scope.Complete();
        return exists;
    }

    public async Task<IReadOnlyCollection<(Guid Id, int PublishedVersion)>> GetPublishedVersionReferencesAsync(
        CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        var results = await scope.ExecuteWithContextAsync(async db =>
            await db.Automations
                .Where(a => a.PublishedVersion != null)
                .Select(a => new { a.Id, PublishedVersion = a.PublishedVersion!.Value })
                .ToListAsync(cancellationToken));

        scope.Complete();
        return results.Select(r => (r.Id, r.PublishedVersion)).ToList();
    }
}
