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
    /// Gets or sets the trigger output data (flattened key-value pairs), or — when the payload
    /// exceeded <c>ExecutionOptions.MaxInlineOutputBytes</c> — a <see cref="StepOutputReference"/>
    /// trigger marker referencing the run whose <c>TriggerData</c> holds it. Binding hydrates the
    /// marker on demand, so a large trigger payload is not re-serialized into the workflow
    /// instance blob on every execution pass.
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
    /// Gets or sets per-iteration step outputs, keyed by the iteration scope path
    /// (see <c>ForEachIterationContext.ScopePath</c>) and then by step id.
    /// Body steps inside a ForEach/While/Parallel iteration record their outputs here so that
    /// later steps in the same iteration resolve <c>${ steps.&lt;alias&gt; }</c> bindings to
    /// their iteration's value rather than whichever iteration most recently overwrote
    /// the global <see cref="StepOutputs"/> entry. Without this, sequential ForEach bodies
    /// that re-run the same step across iterations would clobber one another's outputs.
    /// </summary>
    public Dictionary<string, Dictionary<Guid, Dictionary<string, object?>>> IterationStepOutputs { get; set; } = [];

    /// <summary>
    /// Gets or sets the most recently completed step id within each iteration scope.
    /// Used by <see cref="BindingDataBuilder"/> to give an iteration-scoped <c>previous</c>
    /// binding rather than reading the run-global <see cref="LastCompletedStepId"/>.
    /// </summary>
    public Dictionary<string, Guid?> IterationLastCompletedStepId { get; set; } = [];

    /// <summary>
    /// Gets or sets each ForEach container's collection binding expression, keyed by container
    /// step id. Stashed by the container on entry so binding-time item resolution can
    /// re-materialise the collection on a cache miss (e.g. resuming after a process restart) —
    /// iteration contexts carry only an index, not the item itself.
    /// </summary>
    public Dictionary<Guid, string> ContainerCollections { get; set; } = [];

    /// <summary>
    /// Gets or sets the execution context carrying the service account identity and workspace info.
    /// </summary>
    public AutomationExecutionContext? ExecutionContext { get; set; }
}
