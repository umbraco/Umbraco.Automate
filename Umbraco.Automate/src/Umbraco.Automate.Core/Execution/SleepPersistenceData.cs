namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// Persistence data stored by WorkflowCore when a step is durably sleeping.
/// Used to identify and resume sleeping steps after the engine picks up the workflow instance.
/// </summary>
internal sealed class SleepPersistenceData
{
    /// <summary>
    /// Gets or sets the step run ID to resume.
    /// </summary>
    public Guid StepRunId { get; set; }

    /// <summary>
    /// Gets or sets the automation run ID.
    /// </summary>
    public Guid RunId { get; set; }

    /// <summary>
    /// Gets or sets the step ID.
    /// </summary>
    public Guid StepId { get; set; }
}
