namespace Umbraco.Automate.Persistence.Runs;

/// <summary>
/// EF Core entity for the step run table.
/// </summary>
internal sealed class StepRunEntity
{
    public Guid Id { get; set; }

    public Guid RunId { get; set; }

    public Guid StepId { get; set; }

    public required string ActionAlias { get; set; }

    public int Status { get; set; }

    public DateTime? StartedUtc { get; set; }

    public DateTime? CompletedUtc { get; set; }

    public string? InputData { get; set; }

    public string? OutputData { get; set; }

    public string? LogEntries { get; set; }

    /// <summary>
    /// The outcome key the step exited through (action, If or Switch). Null when none was recorded.
    /// </summary>
    public string? BranchOutcome { get; set; }

    /// <summary>
    /// The zero-based index of the first item a sequential ForEach ran. Null when none ran,
    /// for parallel ForEach, While and other steps.
    /// </summary>
    public int? IterationIndex { get; set; }

    /// <summary>
    /// ForEach: the collection's item count. While: the iterations that ran. Null for other steps.
    /// </summary>
    public int? IterationTotal { get; set; }

    public string? Error { get; set; }

    public int? ErrorCategory { get; set; }

    public int RetryCount { get; set; }

    public long? DurationTicks { get; set; }
}
