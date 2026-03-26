namespace Umbraco.Automate.Core.Runs;

/// <summary>
/// Tracks when the run cleanup job last completed successfully.
/// Used by <see cref="HealthChecks.DataRetentionHealthCheck"/> to verify the job is on schedule.
/// </summary>
public sealed class RunCleanupTracker
{
    private long _lastSuccessfulRunTicks;

    /// <summary>
    /// Gets the UTC time of the last successful cleanup run, or null if it hasn't run yet.
    /// </summary>
    public DateTime? LastSuccessfulRunUtc
    {
        get
        {
            var ticks = Interlocked.Read(ref _lastSuccessfulRunTicks);
            return ticks == 0 ? null : new DateTime(ticks, DateTimeKind.Utc);
        }
    }

    /// <summary>
    /// Records a successful cleanup run.
    /// </summary>
    internal void RecordSuccess()
        => Interlocked.Exchange(ref _lastSuccessfulRunTicks, DateTime.UtcNow.Ticks);
}
