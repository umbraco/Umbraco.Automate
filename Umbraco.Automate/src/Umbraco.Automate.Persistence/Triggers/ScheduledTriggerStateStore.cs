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

        // EF Core does not preserve DateTimeKind across the DB round-trip; the column
        // is UTC by contract, so re-attach the kind for callers that require it
        // (e.g. Cronos rejects DateTime values with Kind=Unspecified).
        return entity is null
            ? null
            : DateTime.SpecifyKind(entity.LastFiredUtc, DateTimeKind.Utc);
    }

    public async Task SetLastFiredAsync(Guid automationId, DateTime firedUtc, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var normalized = firedUtc.Kind == DateTimeKind.Utc
            ? firedUtc
            : firedUtc.ToUniversalTime();

        var entity = await db.ScheduledTriggerStates
            .FirstOrDefaultAsync(e => e.AutomationId == automationId, cancellationToken);

        if (entity is null)
        {
            entity = new ScheduledTriggerStateEntity
            {
                AutomationId = automationId,
                LastFiredUtc = normalized,
            };
            db.ScheduledTriggerStates.Add(entity);
        }
        else
        {
            entity.LastFiredUtc = normalized;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
