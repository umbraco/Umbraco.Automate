using Microsoft.EntityFrameworkCore;
using Umbraco.Automate.Core.Triggers.Scheduling;

namespace Umbraco.Automate.Persistence.Triggers;

/// <summary>
/// EF Core implementation of <see cref="IScheduledTriggerStateStore"/>.
/// </summary>
internal sealed class ScheduledTriggerStateStore : IScheduledTriggerStateStore
{
    private readonly IDbContextFactory<UmbracoAutomateDbContext> _dbContextFactory;

    public ScheduledTriggerStateStore(IDbContextFactory<UmbracoAutomateDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<DateTime?> GetLastFiredAsync(Guid automationId, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await db.ScheduledTriggerStates
            .FirstOrDefaultAsync(e => e.AutomationId == automationId, cancellationToken);

        return entity?.LastFiredUtc;
    }

    public async Task SetLastFiredAsync(Guid automationId, DateTime firedUtc, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await db.ScheduledTriggerStates
            .FirstOrDefaultAsync(e => e.AutomationId == automationId, cancellationToken);

        if (entity is null)
        {
            entity = new ScheduledTriggerStateEntity
            {
                AutomationId = automationId,
                LastFiredUtc = firedUtc,
            };
            db.ScheduledTriggerStates.Add(entity);
        }
        else
        {
            entity.LastFiredUtc = firedUtc;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
