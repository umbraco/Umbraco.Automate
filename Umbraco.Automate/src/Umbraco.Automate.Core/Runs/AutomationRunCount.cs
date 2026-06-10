namespace Umbraco.Automate.Core.Runs;

/// <summary>
/// Run count breakdown for a single automation.
/// </summary>
public sealed class AutomationRunCount
{
    /// <summary>
    /// Gets the automation ID.
    /// </summary>
    public Guid AutomationId { get; init; }

    /// <summary>
    /// Gets the automation name.
    /// </summary>
    public string AutomationName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the total number of runs.
    /// </summary>
    public int TotalRuns { get; init; }

    /// <summary>
    /// Gets the number of successful runs.
    /// </summary>
    public int SuccessCount { get; init; }

    /// <summary>
    /// Gets the number of failed runs.
    /// </summary>
    public int FailCount { get; init; }
}
