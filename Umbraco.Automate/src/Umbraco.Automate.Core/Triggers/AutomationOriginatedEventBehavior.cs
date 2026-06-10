namespace Umbraco.Automate.Core.Triggers;

/// <summary>
/// Controls how a trigger handles events that were dispatched as a side effect of another
/// automation run. Surfaced on settings classes that implement
/// <see cref="IAutomationOriginatedEventBehavior"/> so each automation can choose a posture.
/// </summary>
public enum AutomationOriginatedEventBehavior
{
    /// <summary>
    /// Always fire, regardless of which automation (if any) caused the event. Use only when
    /// chained automations across saves are intentional and you've verified there's no cycle.
    /// </summary>
    Run = 0,

    /// <summary>
    /// Skip when accepting the event would re-enter this automation — i.e. its ID already
    /// appears in the cascade chain. Catches direct self-loops (A → A) and indirect cycles
    /// (A → B → A) while still allowing observer automations downstream. Default.
    /// </summary>
    SkipOnCycle = 1,

    /// <summary>
    /// Skip whenever any automation caused the event, even when no cycle would form. Use for
    /// triggers that should only fire on external input (user, API, webhook) and never as a
    /// side effect of an automation.
    /// </summary>
    SkipAlways = 2,
}
