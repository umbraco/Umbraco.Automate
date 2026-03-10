namespace Umbraco.Automate.Core.Automations;

/// <summary>
/// A connection between two steps on the canvas, defining execution flow.
/// </summary>
public sealed class StepConnection
{
    /// <summary>
    /// Gets or sets the source step ID.
    /// </summary>
    public required Guid SourceStepId { get; set; }

    /// <summary>
    /// Gets or sets the React Flow source handle ID, if any.
    /// </summary>
    public string? SourceHandle { get; set; }

    /// <summary>
    /// Gets or sets the target step ID.
    /// </summary>
    public required Guid TargetStepId { get; set; }

    /// <summary>
    /// Gets or sets the React Flow target handle ID, if any.
    /// </summary>
    public string? TargetHandle { get; set; }

    /// <summary>
    /// Gets or sets the named outcome that activates this connection (e.g. "true", "false").
    /// </summary>
    public string? Outcome { get; set; }
}
