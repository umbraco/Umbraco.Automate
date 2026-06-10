using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.StepTypes;

namespace Umbraco.Automate.Core.ControlFlow;

/// <summary>
/// Infrastructure required by <see cref="ControlFlowBase{TSettings}"/>.
/// Inherits from <see cref="StepTypeInfrastructure"/> to provide shared settings resolution.
/// </summary>
public sealed class ControlFlowInfrastructure : StepTypeInfrastructure
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ControlFlowInfrastructure"/> class.
    /// </summary>
    public ControlFlowInfrastructure(IEditableModelResolver modelResolver)
        : base(modelResolver) { }
}
