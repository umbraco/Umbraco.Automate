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
    /// Gets or sets the trigger output data (flattened key-value pairs).
    /// </summary>
    public Dictionary<string, object?> TriggerOutput { get; set; } = [];

    /// <summary>
    /// Gets or sets accumulated step outputs keyed by step ID.
    /// Each step can produce output that subsequent steps reference via expressions.
    /// </summary>
    public Dictionary<Guid, Dictionary<string, object?>> StepOutputs { get; set; } = [];
}
