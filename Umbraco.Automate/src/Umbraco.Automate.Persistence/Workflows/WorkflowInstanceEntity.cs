namespace Umbraco.Automate.Persistence.Workflows;

/// <summary>
/// EF Core entity for a WorkflowCore workflow instance.
/// </summary>
internal sealed class WorkflowInstanceEntity
{
    public required string Id { get; set; }

    public required string WorkflowDefinitionId { get; set; }

    public int Version { get; set; }

    public int Status { get; set; }

    public string? Description { get; set; }

    public string? Reference { get; set; }

    public DateTime CreateTime { get; set; }

    public long? NextExecution { get; set; }

    public DateTime? CompleteTime { get; set; }

    /// <summary>
    /// JSON-serialized WorkflowInstance (includes ExecutionPointers, Data, etc.).
    /// </summary>
    public required string Data { get; set; }
}
