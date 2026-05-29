using Microsoft.EntityFrameworkCore;
using Umbraco.Automate.Core.Automations;

namespace Umbraco.Automate.Persistence.Automations;

/// <summary>
/// EF Core implementation of <see cref="IAutomationHealthRepository"/>.
/// </summary>
internal sealed class EFCoreAutomationHealthRepository : IAutomationHealthRepository
{
    private readonly IDbContextFactory<UmbracoAutomateDbContext> _dbContextFactory;

    public EFCoreAutomationHealthRepository(IDbContextFactory<UmbracoAutomateDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<AutomationHealthState?> GetAsync(Guid automationId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await db.AutomationHealth
            .FirstOrDefaultAsync(e => e.AutomationId == automationId, cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task SaveAsync(AutomationHealthState state, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await db.AutomationHealth
            .FirstOrDefaultAsync(e => e.AutomationId == state.AutomationId, cancellationToken);

        if (entity is null)
        {
            db.AutomationHealth.Add(new AutomationHealthEntity
            {
                AutomationId = state.AutomationId,
                Health = (int)state.Health,
                WarningIssuedUtc = state.WarningIssuedUtc,
                DisabledUtc = state.DisabledUtc,
                WindowResetUtc = state.WindowResetUtc,
            });
        }
        else
        {
            entity.Health = (int)state.Health;
            entity.WarningIssuedUtc = state.WarningIssuedUtc;
            entity.DisabledUtc = state.DisabledUtc;
            entity.WindowResetUtc = state.WindowResetUtc;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, AutomationHealthState>> GetManyAsync(
        IReadOnlyCollection<Guid> automationIds,
        CancellationToken cancellationToken = default)
    {
        if (automationIds.Count == 0)
        {
            return new Dictionary<Guid, AutomationHealthState>();
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entities = await db.AutomationHealth
            .Where(e => automationIds.Contains(e.AutomationId))
            .ToListAsync(cancellationToken);

        return entities.ToDictionary(e => e.AutomationId, ToDomain);
    }

    private static AutomationHealthState ToDomain(AutomationHealthEntity entity) => new()
    {
        AutomationId = entity.AutomationId,
        Health = (AutomationHealth)entity.Health,
        WarningIssuedUtc = entity.WarningIssuedUtc,
        DisabledUtc = entity.DisabledUtc,
        WindowResetUtc = entity.WindowResetUtc,
    };
}
