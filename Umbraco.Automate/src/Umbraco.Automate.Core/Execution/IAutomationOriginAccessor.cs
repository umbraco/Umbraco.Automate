namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// Provides access to the ambient <see cref="AutomationOrigin"/> for the current async flow.
/// Set by action middleware while an action is executing; read by the trigger dispatch path
/// when notifications fire as a side effect of that action.
/// </summary>
public interface IAutomationOriginAccessor
{
    /// <summary>
    /// Gets the ambient origin for the current async flow, or <c>null</c> when not running
    /// inside an automation action.
    /// </summary>
    AutomationOrigin? Current { get; }

    /// <summary>
    /// Pushes a new origin onto the current async flow. Disposing the returned scope restores
    /// the previous origin (so nested scopes are safe).
    /// </summary>
    IDisposable Push(AutomationOrigin origin);
}
