using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.StepTypes;

/// <summary>
/// Shared infrastructure required by <see cref="StepTypeBase{TSettings}"/>.
/// Wraps the <see cref="IEditableModelResolver"/> to avoid leaking infrastructure concerns.
/// Domain-specific infrastructure classes inherit from this.
/// </summary>
public class StepTypeInfrastructure
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StepTypeInfrastructure"/> class.
    /// </summary>
    public StepTypeInfrastructure(IEditableModelResolver modelResolver)
    {
        ModelResolver = modelResolver;
    }

    internal IEditableModelResolver ModelResolver { get; }
}
