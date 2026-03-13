namespace Umbraco.Automate.Core.Runs;

/// <summary>
/// Aggregate summary of automation run statistics.
/// </summary>
public sealed class RunSummary
{
    /// <summary>
    /// Gets the total number of runs.
    /// </summary>
    public int TotalRuns { get; init; }

    /// <summary>
    /// Gets the count of runs grouped by status.
    /// </summary>
    public Dictionary<AutomationRunStatus, int> ByStatus { get; init; } = new();

    /// <summary>
    /// Gets the success rate as a decimal (0.0 to 1.0).
    /// </summary>
    public decimal SuccessRate { get; init; }
}
