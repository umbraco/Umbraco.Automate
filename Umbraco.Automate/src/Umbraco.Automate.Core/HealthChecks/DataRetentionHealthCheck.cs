using Umbraco.Automate.Core.Runs;
using Umbraco.Cms.Core.HealthChecks;

namespace Umbraco.Automate.Core.HealthChecks;

/// <summary>
/// Checks that the run cleanup purge job has executed successfully within the expected interval.
/// </summary>
[HealthCheck(
    "D4E5F6A7-8B9C-4D1E-A2F3-B4C5D6E7F8A9",
    "Data Retention",
    Description = "Checks that the automation run cleanup job is running on schedule.",
    Group = "Umbraco Automate")]
public sealed class DataRetentionHealthCheck : HealthCheck
{
    private readonly RunCleanupTracker _tracker;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataRetentionHealthCheck"/> class.
    /// </summary>
    public DataRetentionHealthCheck(RunCleanupTracker tracker)
    {
        _tracker = tracker;
    }

    /// <inheritdoc />
    public override Task<IEnumerable<HealthCheckStatus>> GetStatusAsync()
    {
        var lastRun = _tracker.LastSuccessfulRunUtc;

        if (lastRun is null)
        {
            // Job hasn't run yet — could be startup delay.
            return Task.FromResult<IEnumerable<HealthCheckStatus>>(
            [
                new HealthCheckStatus("The data retention cleanup job has not run yet. It may still be within its startup delay.")
                {
                    ResultType = StatusResultType.Info,
                },
            ]);
        }

        var elapsed = DateTime.UtcNow - lastRun.Value;

        // The job runs every 12 hours; allow 2x the interval before warning.
        if (elapsed > TimeSpan.FromHours(36))
        {
            return Task.FromResult<IEnumerable<HealthCheckStatus>>(
            [
                new HealthCheckStatus($"The data retention cleanup job last ran {elapsed.TotalHours:F0} hours ago, which exceeds the expected interval.")
                {
                    ResultType = StatusResultType.Warning,
                },
            ]);
        }

        return Task.FromResult<IEnumerable<HealthCheckStatus>>(
        [
            new HealthCheckStatus($"The data retention cleanup job last ran {elapsed.TotalHours:F1} hours ago.")
            {
                ResultType = StatusResultType.Success,
            },
        ]);
    }
}
