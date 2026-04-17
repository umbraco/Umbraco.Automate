namespace Umbraco.Automate.Core.Runs;

/// <summary>
/// Outcome of a run lifecycle operation (suspend / resume / terminate).
/// </summary>
public enum RunLifecycleResult
{
    /// <summary>The operation completed successfully.</summary>
    Success = 0,

    /// <summary>The run does not exist.</summary>
    NotFound = 1,

    /// <summary>The run's current status does not allow the requested transition.</summary>
    InvalidState = 2,

    /// <summary>The run has no persisted WorkflowCore instance ID (pre-migration or start failure).</summary>
    NoWorkflowInstance = 3,
}
