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
}
