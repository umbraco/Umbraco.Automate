using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Actions;

/// <summary>
/// Infrastructure infrastructure required by <see cref="ActionBase{TSettings}"/>.
/// Passed as a single envelope to avoid leaking infrastructure concerns into action implementations.
/// </summary>
public sealed class ActionInfrastructure
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActionInfrastructure"/> class.
    /// </summary>
    public ActionInfrastructure(IEditableModelResolver modelResolver)
    {
        ModelResolver = modelResolver;
    }

    internal IEditableModelResolver ModelResolver { get; }
}
