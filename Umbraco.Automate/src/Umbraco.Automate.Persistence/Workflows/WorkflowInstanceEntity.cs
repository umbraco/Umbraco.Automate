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
    /// Storage format discriminator for the <see cref="Data"/> column.
    /// <list type="bullet">
    /// <item><c>0</c> (legacy): <see cref="Data"/> is the whole JSON-serialized WorkflowInstance
    /// (execution pointers inlined). Written before pointer normalization; read via the legacy
    /// fallback and rewritten as version 1 on the next persist.</item>
    /// <item><c>1</c> (normalized): <see cref="Data"/> holds only the workflow's <c>Data</c>
    /// payload; execution pointers live in <see cref="ExecutionPointers"/>.</item>
    /// </list>
    /// </summary>
    public int SchemaVersion { get; set; }

    /// <summary>
    /// For <see cref="SchemaVersion"/> 1, the JSON-serialized workflow <c>Data</c> payload only.
    /// For legacy version 0 rows, the whole JSON-serialized WorkflowInstance.
    /// </summary>
    public required string Data { get; set; }

    /// <summary>
    /// The workflow's execution pointers (version 1 only; empty for legacy version 0 rows,
    /// whose pointers are still inlined in <see cref="Data"/>).
    /// </summary>
    public List<WorkflowExecutionPointerEntity> ExecutionPointers { get; set; } = [];
}
