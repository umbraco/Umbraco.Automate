using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Messaging;
using Umbraco.Cms.Core.HealthChecks;

namespace Umbraco.Automate.Core.HealthChecks;

/// <summary>
/// Checks that the outbox work queue depth is below the configured maximum.
/// Warns at 80% capacity; errors when the limit is exceeded.
/// </summary>
[HealthCheck(
    "B3E4F5A6-7C8D-4E9F-A1B2-C3D4E5F6A7B8",
    "Automation Queue Depth",
    Description = "Checks that the automation message queue is not overloaded.",
    Group = "Umbraco Automate")]
public sealed class QueueDepthHealthCheck : HealthCheck
{
    private readonly IAutomateHealthService _healthService;
    private readonly IOptions<OutboxOptions> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueueDepthHealthCheck"/> class.
    /// </summary>
    public QueueDepthHealthCheck(IAutomateHealthService healthService, IOptions<OutboxOptions> options)
    {
        _healthService = healthService;
        _options = options;
    }

    /// <inheritdoc />
    public override async Task<IEnumerable<HealthCheckStatus>> GetStatusAsync()
    {
        var stats = await _healthService.GetQueueStatsAsync(CancellationToken.None);
        var maxDepth = _options.Value.MaxPendingMessages;
        var warningThreshold = (int)(maxDepth * 0.8);

        if (stats.Pending >= maxDepth)
        {
            return [new HealthCheckStatus(
                $"Queue depth critical: {stats.Pending} pending messages (limit: {maxDepth}). New automation runs may be rejected.")
            {
                ResultType = StatusResultType.Error,
            }];
        }

        if (stats.Pending >= warningThreshold)
        {
            return [new HealthCheckStatus(
                $"Queue depth elevated: {stats.Pending} pending messages (warning at {warningThreshold}, limit: {maxDepth}).")
            {
                ResultType = StatusResultType.Warning,
            }];
        }

        if (stats.DeadLettered > 0)
        {
            return [new HealthCheckStatus(
                $"Queue is healthy ({stats.Pending} pending) but {stats.DeadLettered} dead-lettered messages need attention.")
            {
                ResultType = StatusResultType.Warning,
            }];
        }

        return [new HealthCheckStatus(
            $"Queue depth is healthy: {stats.Pending} pending messages.")
        {
            ResultType = StatusResultType.Success,
        }];
    }
}
