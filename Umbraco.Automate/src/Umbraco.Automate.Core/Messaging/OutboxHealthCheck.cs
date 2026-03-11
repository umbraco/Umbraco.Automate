using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Umbraco.Automate.Core.Messaging;

/// <summary>
/// Reports the health of the outbox message queue.
/// Degraded when dead-lettered messages exist; unhealthy when the pending queue exceeds a threshold.
/// </summary>
internal sealed class OutboxHealthCheck : IHealthCheck
{
    private const int DegradedPendingThreshold = 100;
    private const int UnhealthyPendingThreshold = 1000;

    private readonly IOutboxStore _store;

    public OutboxHealthCheck(IOutboxStore store)
    {
        _store = store;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var stats = await _store.GetStatsAsync(cancellationToken);

        var data = new Dictionary<string, object>
        {
            ["pending"] = stats.Pending,
            ["processing"] = stats.Processing,
            ["failed"] = stats.Failed,
            ["deadLettered"] = stats.DeadLettered,
        };

        if (stats.Pending >= UnhealthyPendingThreshold)
        {
            return HealthCheckResult.Unhealthy(
                $"Outbox backlog critical: {stats.Pending} pending messages",
                data: data);
        }

        if (stats.DeadLettered > 0 || stats.Pending >= DegradedPendingThreshold)
        {
            return HealthCheckResult.Degraded(
                $"Outbox: {stats.Pending} pending, {stats.DeadLettered} dead-lettered",
                data: data);
        }

        return HealthCheckResult.Healthy("Outbox is operating normally", data: data);
    }
}
