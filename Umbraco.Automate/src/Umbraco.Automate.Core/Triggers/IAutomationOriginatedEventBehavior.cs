namespace Umbraco.Automate.Core.Triggers;

/// <summary>
/// Implemented by trigger settings that configure how the trigger reacts to events
/// originated by an automation run. The dispatch path consults this on each receiving
/// automation when a stamped event arrives.
/// </summary>
public interface IAutomationOriginatedEventBehavior
{
    /// <summary>
    /// Gets the behaviour to apply when the incoming event was caused by another automation.
    /// Implementations storing the value as a string (e.g. for dropdown round-trips) should
    /// parse it here and fall back to <see cref="AutomationOriginatedEventBehavior.SkipOnCycle"/>
    /// on unknown input.
    /// </summary>
    AutomationOriginatedEventBehavior OnAutomationOriginated { get; }
}
