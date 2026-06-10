using Umbraco.Automate.Core.StepTypes;

namespace Umbraco.Automate.Core.Triggers;

/// <summary>
/// Defines an automation trigger — the event that starts an automation.
/// Triggers are discovered at startup and registered in the trigger catalogue.
/// </summary>
public interface ITrigger : IStepType
{
    /// <summary>
    /// Determines whether this trigger should handle the given event output for an automation
    /// configured with the given settings. Called by the dispatcher for each subscribing
    /// automation to filter events based on that automation's trigger settings
    /// (e.g. <c>ContentTypes</c> on content triggers).
    /// Defaults to <c>true</c> — triggers without filterable settings need not override.
    /// </summary>
    /// <param name="output">The trigger event output.</param>
    /// <param name="settings">The automation's resolved trigger settings, or <c>null</c> if unconfigured.</param>
    /// <returns><c>true</c> if the event should fire for an automation with these settings; otherwise <c>false</c>.</returns>
    bool CanHandle(object output, object? settings) => true;
}
