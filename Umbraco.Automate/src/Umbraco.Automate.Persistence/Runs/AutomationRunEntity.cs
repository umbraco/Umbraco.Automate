namespace Umbraco.Automate.Persistence.Runs;

/// <summary>
/// EF Core entity for the automation run table.
/// </summary>
internal sealed class AutomationRunEntity
{
    public Guid Id { get; set; }

    public Guid AutomationId { get; set; }

    public int AutomationVersion { get; set; }

    public Guid WorkspaceId { get; set; }

    public Guid ServiceAccountKey { get; set; }

    public int Status { get; set; }

    public DateTime? StartedUtc { get; set; }

    public DateTime? CompletedUtc { get; set; }

    public string? TriggerData { get; set; }

    public required string InitiatedBy { get; set; }

    public string? CorrelationId { get; set; }

    public string? Error { get; set; }

    public string? WorkflowInstanceId { get; set; }
}
