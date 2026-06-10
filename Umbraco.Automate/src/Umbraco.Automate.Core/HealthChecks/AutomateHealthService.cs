using Umbraco.Automate.Core.Messaging;

namespace Umbraco.Automate.Core.HealthChecks;

/// <summary>
/// Default implementation of <see cref="IAutomateHealthService"/> backed by the outbox store.
/// </summary>
internal sealed class AutomateHealthService : IAutomateHealthService
{
    private readonly IOutboxStore _outboxStore;

    public AutomateHealthService(IOutboxStore outboxStore)
    {
        _outboxStore = outboxStore;
    }

    public async Task<AutomateQueueStats> GetQueueStatsAsync(CancellationToken cancellationToken = default)
    {
        var stats = await _outboxStore.GetStatsAsync(cancellationToken);
        return new AutomateQueueStats
        {
            Pending = stats.Pending,
            Processing = stats.Processing,
            Failed = stats.Failed,
            DeadLettered = stats.DeadLettered,
        };
    }
}
