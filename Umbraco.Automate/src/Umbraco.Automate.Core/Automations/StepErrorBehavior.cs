namespace Umbraco.Automate.Core.Automations;

/// <summary>
/// Defines the behavior when a step encounters an error.
/// </summary>
public enum StepErrorBehavior
{
    /// <summary>
    /// Retry the step according to configured retry settings.
    /// </summary>
    Retry = 0,

    /// <summary>
    /// Suspend the run for manual intervention.
    /// </summary>
    Suspend = 1,

    /// <summary>
    /// Terminate the entire run immediately.
    /// </summary>
    Terminate = 2,

    /// <summary>
    /// Run a compensation step before terminating.
    /// </summary>
    Compensate = 3,
}
