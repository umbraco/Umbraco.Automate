namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// Data object passed through a WorkflowCore workflow instance, carrying
/// per-run state that flows between steps.
/// </summary>
public sealed class AutomationWorkflowData
{
    /// <summary>
    /// Gets or sets the automation run ID.
    /// </summary>
    public Guid RunId { get; set; }

    /// <summary>
    /// Gets or sets the automation ID.
    /// </summary>
    public Guid AutomationId { get; set; }

    /// <summary>
    /// Gets or sets the automation alias (used for metrics tagging).
    /// </summary>
    public string AutomationAlias { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the trigger output data (flattened key-value pairs).
    /// </summary>
    public Dictionary<string, object?> TriggerOutput { get; set; } = [];

    /// <summary>
    /// Gets or sets accumulated step outputs keyed by step ID.
    /// Each step can produce output that subsequent steps reference via expressions.
    /// </summary>
    public Dictionary<Guid, Dictionary<string, object?>> StepOutputs { get; set; } = [];

    /// <summary>
    /// Gets or sets a mapping from step ID to step alias.
    /// Populated at workflow start from the automation definition.
    /// </summary>
    public Dictionary<Guid, string> StepAliases { get; set; } = [];

    /// <summary>
    /// Gets or sets the ID of the most recently completed step that produced output.
    /// Used by <see cref="BindingDataBuilder"/> to populate the <c>previous</c> binding key.
    /// </summary>
    public Guid? LastCompletedStepId { get; set; }

    /// <summary>
    /// Gets or sets the execution context carrying the service account identity and workspace info.
    /// </summary>
    public AutomationExecutionContext? ExecutionContext { get; set; }
}
