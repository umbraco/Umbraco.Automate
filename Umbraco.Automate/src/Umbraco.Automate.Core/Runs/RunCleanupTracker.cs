namespace Umbraco.Automate.Core.Runs;

/// <summary>
/// Tracks when the run cleanup job last completed successfully.
/// Used by <see cref="HealthChecks.DataRetentionHealthCheck"/> to verify the job is on schedule.
/// </summary>
public sealed class RunCleanupTracker
{
    /// <summary>
    /// Gets the UTC time of the last successful cleanup run, or null if it hasn't run yet.
    /// </summary>
    public DateTime? LastSuccessfulRunUtc { get; private set; }

    /// <summary>
    /// Records a successful cleanup run.
    /// </summary>
    internal void RecordSuccess() => LastSuccessfulRunUtc = DateTime.UtcNow;
}
