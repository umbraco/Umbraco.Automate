namespace Umbraco.Automate.Core.Triggers;

/// <summary>
/// Marker interface for trigger settings that opt in to skipping events stamped with an
/// automation origin. Implemented by built-in content trigger settings (saved, published,
/// unpublished and their batch variants) so the dispatch path can drop loop-back events
/// without each trigger re-implementing the check.
/// </summary>
public interface ISkipAutomationOriginatedEvents
{
    /// <summary>
    /// Gets a value indicating whether the trigger should skip events that were dispatched
    /// as a side effect of another automation run. When <c>true</c>, the dispatch path
    /// drops the event before starting the run.
    /// </summary>
    bool SkipAutomationOriginatedEvents { get; }
}
