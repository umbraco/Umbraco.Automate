namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Output produced by the <see cref="StartAutomationAction"/>.
/// </summary>
public sealed class StartAutomationOutput
{
    /// <summary>
    /// Gets the key of the automation that was started.
    /// </summary>
    public Guid AutomationKey { get; init; }

    /// <summary>
    /// Gets the id of the run that was started, or null when no run was started.
    /// </summary>
    public Guid? RunId { get; init; }

    /// <summary>
    /// Gets a value indicating whether a run was actually started.
    /// </summary>
    public bool Started { get; init; }

    /// <summary>
    /// Gets the reason no run was started, when <see cref="Started"/> is false.
    /// </summary>
    public string? SkippedReason { get; init; }
}
