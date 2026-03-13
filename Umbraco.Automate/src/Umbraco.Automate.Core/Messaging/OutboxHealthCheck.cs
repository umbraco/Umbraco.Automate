using Microsoft.Extensions.Diagnostics.HealthChecks;
using Umbraco.Automate.Core.Persistence;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Services;

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
    private readonly IRuntimeState _runtimeState;

    /// <summary>
    /// Set to <c>true</c> once Automate EF Core migrations have completed successfully.
    /// Used to prevent health check queries before the outbox table exists.
    /// </summary>
    internal static volatile bool MigrationsComplete;

    public OutboxHealthCheck(IOutboxStore store, IRuntimeState runtimeState)
    {
        _store = store;
        _runtimeState = runtimeState;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (_runtimeState.Level < RuntimeLevel.Run || !MigrationsComplete)
        {
            return HealthCheckResult.Degraded("Automate migrations have not yet completed");
        }

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
