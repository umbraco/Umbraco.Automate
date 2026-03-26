using Microsoft.EntityFrameworkCore;
using Umbraco.Automate.Core;
using Umbraco.Automate.Core.Automations;

namespace Umbraco.Automate.Persistence.Automations;

/// <summary>
/// EF Core implementation of <see cref="IAutomationRepository"/>.
/// </summary>
internal sealed class EFCoreAutomationRepository : IAutomationRepository
{
    private readonly IDbContextFactory<UmbracoAutomateDbContext> _dbContextFactory;
    private readonly AutomationFactory _factory;

    public EFCoreAutomationRepository(
        IDbContextFactory<UmbracoAutomateDbContext> dbContextFactory,
        AutomationFactory factory)
    {
        _dbContextFactory = dbContextFactory;
        _factory = factory;
    }

    public async Task<Automation?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        AutomationEntity? entity = await db.Automations
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        return entity is null ? null : _factory.BuildDomain(entity);
    }

    public async Task<Automation?> GetByAliasAsync(string alias, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        AutomationEntity? entity = await db.Automations
            .FirstOrDefaultAsync(a => a.Alias == alias, cancellationToken);

        return entity is null ? null : _factory.BuildDomain(entity);
    }

    public async Task<IEnumerable<Automation>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entities = await db.Automations
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);

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
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

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

        return (items.Select(_factory.BuildDomain), total);
    }

    public async Task<Automation> SaveAsync(Automation automation, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

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
            // Set EF Core's original value to the client's version so the WHERE clause
            // detects races between the controller read and this repository save.
            db.Entry(existing).Property(e => e.Version).OriginalValue = automation.Version;

            automation.Version = existing.Version + 1;
            automation.DateModified = DateTime.UtcNow;
            automation.ModifiedByUserId = userId;

            _factory.UpdateEntity(existing, automation);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException(nameof(Automation), automation.Id);
        }

        return automation;
    }

    public async Task<Automation> SaveMetadataAsync(Automation automation, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        AutomationEntity? existing = await db.Automations.FindAsync([automation.Id], cancellationToken)
            ?? throw new InvalidOperationException($"Automation '{automation.Id}' not found.");

        db.Entry(existing).Property(e => e.Version).OriginalValue = automation.Version;

        automation.DateModified = DateTime.UtcNow;
        automation.ModifiedByUserId = userId;

        _factory.UpdateMetadata(existing, automation);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException(nameof(Automation), automation.Id);
        }

        return automation;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        AutomationEntity? entity = await db.Automations.FindAsync([id], cancellationToken);
        if (entity is null)
        {
            return false;
        }

        db.Automations.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Automations.AnyAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<(Guid Id, int PublishedVersion)>> GetPublishedVersionReferencesAsync(
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var results = await db.Automations
            .Where(a => a.PublishedVersion != null)
            .Select(a => new { a.Id, PublishedVersion = a.PublishedVersion!.Value })
            .ToListAsync(cancellationToken);

        return results.Select(r => (r.Id, r.PublishedVersion)).ToList();
    }
}
