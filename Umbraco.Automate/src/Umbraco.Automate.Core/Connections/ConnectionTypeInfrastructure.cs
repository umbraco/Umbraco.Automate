using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Connections;

/// <summary>
/// Infrastructure required by <see cref="ConnectionTypeBase{TSettings}"/>.
/// Passed as a single envelope to avoid leaking infrastructure concerns into connection type implementations.
/// </summary>
public sealed class ConnectionTypeInfrastructure
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionTypeInfrastructure"/> class.
    /// </summary>
    public ConnectionTypeInfrastructure(IEditableModelResolver modelResolver)
    {
        ModelResolver = modelResolver;
    }

    internal IEditableModelResolver ModelResolver { get; }
}
