namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// Thrown when a run is requested on a node whose WorkflowCore host was left stopped because
/// startup recovery failed. Refusing the run is better than creating one that never executes.
/// </summary>
internal sealed class AutomationEngineNotRunningException : InvalidOperationException
{
    public AutomationEngineNotRunningException()
        : base(
            "The automation engine is not running on this server because startup recovery failed. " +
            "Check the log for the error, resolve the cause and restart the site.")
    {
    }
}
