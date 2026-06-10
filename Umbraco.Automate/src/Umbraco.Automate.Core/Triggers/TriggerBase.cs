using Umbraco.Automate.Core.StepTypes;

namespace Umbraco.Automate.Core.Triggers;

/// <summary>
/// Base class for triggers that reads metadata from the <see cref="TriggerAttribute"/>
/// and auto-derives settings and output schemas.
/// </summary>
/// <typeparam name="TSettings">The settings POCO type (use <see cref="object"/> if no settings).</typeparam>
/// <typeparam name="TOutput">The output POCO type (use <see cref="object"/> if no output).</typeparam>
public abstract class TriggerBase<TSettings, TOutput> : StepTypeBase<TSettings, TOutput, TriggerAttribute, TriggerInfrastructure>, ITrigger
    where TSettings : class, new()
    where TOutput : class
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TriggerBase{TSettings, TOutput}"/> class.
    /// </summary>
    protected TriggerBase(TriggerInfrastructure infrastructure) : base(infrastructure) { }

    /// <summary>
    /// Override to filter events based on the automation's trigger settings. The default
    /// implementation returns <c>true</c>, meaning every event fires for every subscribing
    /// automation. Triggers with filterable settings (e.g. <c>ContentTypes</c>) should
    /// override to compare <paramref name="output"/> against <paramref name="settings"/>.
    /// </summary>
    /// <param name="output">The trigger event output.</param>
    /// <param name="settings">The automation's resolved trigger settings, or <c>null</c> if unconfigured.</param>
    /// <returns><c>true</c> if the event should fire for this automation.</returns>
    protected virtual bool CanHandle(TOutput output, TSettings? settings) => true;

    /// <inheritdoc />
    // Defensive: if the dispatcher ever hands us the wrong output type, don't filter
    // rather than dropping events — mis-typed deserialization should not silently
    // suppress automations.
    bool ITrigger.CanHandle(object output, object? settings)
        => output is not TOutput typed || CanHandle(typed, settings as TSettings);
}
