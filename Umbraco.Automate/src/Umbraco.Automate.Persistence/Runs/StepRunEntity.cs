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

    public string? Error { get; set; }

    public int? ErrorCategory { get; set; }

    public int RetryCount { get; set; }

    public long? DurationTicks { get; set; }
}
