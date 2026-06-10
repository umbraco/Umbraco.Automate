namespace Umbraco.Automate.Core.Runs;

/// <summary>
/// Result of a run cleanup operation.
/// </summary>
public sealed class RunCleanupResult
{
    /// <summary>
    /// Gets the number of runs deleted due to exceeding the age threshold.
    /// </summary>
    public int DeletedByAge { get; init; }

    /// <summary>
    /// Gets the number of runs deleted due to exceeding the count limit per automation.
    /// </summary>
    public int DeletedByCount { get; init; }

    /// <summary>
    /// Gets the total number of runs deleted.
    /// </summary>
    public int TotalDeleted => DeletedByAge + DeletedByCount;

    /// <summary>
    /// Gets a value indicating whether the cleanup was skipped.
    /// </summary>
    public bool WasSkipped { get; init; }

    /// <summary>
    /// Gets the reason why cleanup was skipped, if applicable.
    /// </summary>
    public string? SkipReason { get; init; }
}
