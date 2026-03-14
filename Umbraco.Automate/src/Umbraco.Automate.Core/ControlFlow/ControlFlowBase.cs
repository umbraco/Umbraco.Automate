using Umbraco.Automate.Core.StepTypes;

namespace Umbraco.Automate.Core.ControlFlow;

/// <summary>
/// Base class for control flow step types that reads metadata from the <see cref="ControlFlowAttribute"/>
/// and auto-derives settings schema.
/// </summary>
/// <typeparam name="TSettings">The settings POCO type (use <see cref="object"/> if no settings).</typeparam>
public abstract class ControlFlowBase<TSettings> : StepTypeBase<TSettings, ControlFlowAttribute, ControlFlowInfrastructure>, IControlFlow
    where TSettings : class, new()
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ControlFlowBase{TSettings}"/> class.
    /// </summary>
    protected ControlFlowBase(ControlFlowInfrastructure infrastructure) : base(infrastructure) { }
}
