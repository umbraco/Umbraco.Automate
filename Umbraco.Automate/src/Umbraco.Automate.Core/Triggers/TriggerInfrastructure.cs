using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.StepTypes;

namespace Umbraco.Automate.Core.Triggers;

/// <summary>
/// Infrastructure required by <see cref="TriggerBase{TSettings, TOutput}"/>.
/// Thin wrapper over <see cref="StepTypeInfrastructure"/> — kept distinct so trigger-specific
/// dependencies can be added later without touching the action/step-type surface.
/// </summary>
public sealed class TriggerInfrastructure : StepTypeInfrastructure
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TriggerInfrastructure"/> class.
    /// </summary>
    public TriggerInfrastructure(IEditableModelResolver modelResolver)
        : base(modelResolver)
    {
    }
}
