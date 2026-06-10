namespace Umbraco.Automate.Core.Runs;

/// <summary>
/// Configuration for automatic cleanup of old automation run records.
/// </summary>
public sealed class RunCleanupPolicy
{
    /// <summary>
    /// Gets or sets a value indicating whether automatic run cleanup is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum age (in days) for run records. Runs older than this are deleted.
    /// Set to 0 or less to disable age-based cleanup.
    /// </summary>
    public int RetentionDays { get; set; } = 90;

    /// <summary>
    /// Gets or sets the maximum number of run records to keep per automation.
    /// Set to 0 or less to disable count-based cleanup.
    /// </summary>
    public int MaxRunsPerAutomation { get; set; } = 1000;
}
