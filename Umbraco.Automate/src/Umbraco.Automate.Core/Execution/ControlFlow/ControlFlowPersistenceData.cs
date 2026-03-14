namespace Umbraco.Automate.Core.Execution.ControlFlow;

/// <summary>
/// Persistence data for control flow step bodies.
/// Used to track iteration state across workflow suspensions.
/// </summary>
internal sealed class ControlFlowPersistenceData
{
    public ControlFlowPersistenceData(string stepType)
    {
        StepType = stepType;
    }

    /// <summary>
    /// Gets the step type name for identification.
    /// </summary>
    public string StepType { get; }

    /// <summary>
    /// Gets or sets optional metadata (e.g. current iteration index).
    /// </summary>
    public string? Metadata { get; set; }
}
