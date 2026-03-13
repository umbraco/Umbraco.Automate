using Microsoft.EntityFrameworkCore;
using Umbraco.Automate.Core.Triggers.Scheduling;

namespace Umbraco.Automate.Persistence.Triggers;

/// <summary>
/// EF Core implementation of <see cref="IScheduledTriggerStateStore"/>.
/// </summary>
internal sealed class ScheduledTriggerStateStore : IScheduledTriggerStateStore
{
    private readonly UmbracoAutomateDbContext _dbContext;

    public ScheduledTriggerStateStore(UmbracoAutomateDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DateTime?> GetLastFiredAsync(Guid automationId, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.ScheduledTriggerStates
            .FirstOrDefaultAsync(e => e.AutomationId == automationId, cancellationToken);

        return entity?.LastFiredUtc;
    }

    public async Task SetLastFiredAsync(Guid automationId, DateTime firedUtc, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.ScheduledTriggerStates
            .FirstOrDefaultAsync(e => e.AutomationId == automationId, cancellationToken);

        if (entity is null)
        {
            entity = new ScheduledTriggerStateEntity
            {
                AutomationId = automationId,
                LastFiredUtc = firedUtc,
            };
            _dbContext.ScheduledTriggerStates.Add(entity);
        }
        else
        {
            entity.LastFiredUtc = firedUtc;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
