namespace Umbraco.Automate.Persistence.Workflows;

/// <summary>
/// EF Core entity for a single WorkflowCore <c>ExecutionPointer</c>.
/// <para>
/// Pointers are stored one row per pointer (rather than inside the owning
/// <see cref="WorkflowInstanceEntity"/> blob) so a persistence pass writes only the pointers
/// that changed, instead of re-serializing the entire — and unboundedly growing — pointer
/// collection on every pass. This mirrors the schema of WorkflowCore's own EF persistence
/// provider (<c>PersistedExecutionPointer</c>).
/// </para>
/// </summary>
internal sealed class WorkflowExecutionPointerEntity
{
    /// <summary>Surrogate key (mirrors WorkflowCore's <c>PersistenceId</c>).</summary>
    public long PersistenceId { get; set; }

    /// <summary>FK to the owning <see cref="WorkflowInstanceEntity.Id"/>.</summary>
    public required string WorkflowInstanceId { get; set; }

    /// <summary>The engine's pointer id (unique within a workflow instance).</summary>
    public required string PointerId { get; set; }

    public int StepId { get; set; }

    public bool Active { get; set; }

    public DateTime? SleepUntil { get; set; }

    public DateTime? StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public int RetryCount { get; set; }

    public string? PredecessorId { get; set; }

    public string? EventName { get; set; }

    public string? EventKey { get; set; }

    public bool EventPublished { get; set; }

    public string? StepName { get; set; }

    /// <summary>Serialized <c>PointerStatus</c> (stored as its integer value).</summary>
    public int Status { get; set; }

    /// <summary>Child pointer ids, joined with ';' (WorkflowCore's persisted format).</summary>
    public string? Children { get; set; }

    /// <summary>Scope stack ids, joined with ';' (WorkflowCore's persisted format).</summary>
    public string? Scope { get; set; }

    /// <summary>JSON (Newtonsoft, TypeNameHandling.All) — polymorphic persistence data.</summary>
    public string? PersistenceData { get; set; }

    /// <summary>JSON (Newtonsoft, TypeNameHandling.All) — polymorphic loop/branch context item.</summary>
    public string? ContextItem { get; set; }

    /// <summary>JSON (Newtonsoft, TypeNameHandling.All) — polymorphic event payload.</summary>
    public string? EventData { get; set; }

    /// <summary>JSON (Newtonsoft, TypeNameHandling.All) — outcome value.</summary>
    public string? Outcome { get; set; }

    /// <summary>
    /// JSON (Newtonsoft, TypeNameHandling.All) — the pointer's extension attributes dictionary.
    /// Stored inline as a single column rather than a child table (as WorkflowCore's native
    /// provider does): nothing in the engine or in Umbraco.Automate populates it, so it is
    /// effectively always empty, and we never query by attribute key.
    /// </summary>
    public string? ExtensionAttributes { get; set; }
}
