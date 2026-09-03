namespace Umbraco.Automate.Core.Triggers;

/// <summary>
/// Implemented by triggers whose automations can be started on demand from the backoffice
/// ("Run now"), rather than only by the event the trigger normally waits on.
/// <para>
/// Triggers that need no payload (e.g. <c>ManualTrigger</c>, <c>ScheduledTrigger</c>) return
/// <see cref="ManualRunOutput.None"/>. Triggers whose steps read a payload can stand in for
/// the real event by building one from their own saved settings, so an automation can be
/// developed and exercised without whatever normally calls it.
/// </para>
/// <para>
/// Implementing this interface is what makes "Run now" available for a trigger — the trigger
/// catalogue reports it, and the manual trigger endpoint asks for the payload. Triggers whose
/// output cannot be faked meaningfully (e.g. content events, which carry a real node) should
/// not implement it.
/// </para>
/// </summary>
public interface ISupportsManualRun
{
    /// <summary>
    /// Builds the stand-in trigger output for an on-demand run.
    /// </summary>
    /// <param name="settings">
    /// The automation's resolved trigger settings, or <c>null</c> if unconfigured.
    /// </param>
    /// <returns>
    /// <see cref="ManualRunOutput.None"/> when no payload is needed, output built from the
    /// settings, or <see cref="ManualRunOutput.Invalid"/> when the settings cannot produce a
    /// payload — the run is then refused rather than started with data the author didn't mean.
    /// </returns>
    ManualRunOutput CreateManualRunOutput(object? settings);
}
