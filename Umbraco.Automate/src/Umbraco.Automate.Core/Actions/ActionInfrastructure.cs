using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.StepTypes;

namespace Umbraco.Automate.Core.Actions;

/// <summary>
/// Infrastructure required by <see cref="ActionBase{TSettings}"/>.
/// Inherits from <see cref="StepTypeInfrastructure"/> to provide shared settings resolution.
/// </summary>
public sealed class ActionInfrastructure : StepTypeInfrastructure
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActionInfrastructure"/> class.
    /// </summary>
    public ActionInfrastructure(IEditableModelResolver modelResolver)
        : base(modelResolver) { }
}
