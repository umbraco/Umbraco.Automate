using DotNetCore.CAP;

namespace Umbraco.Automate.Persistence.Transport;

/// <summary>
/// Extension methods for configuring CAP to use the database-backed transport.
/// </summary>
internal static class CapOptionsExtensions
{
    /// <summary>
    /// Uses the shared Umbraco database as the CAP message transport.
    /// All nodes poll the same table — no external broker required.
    /// </summary>
    public static CapOptions UseDatabaseTransport(this CapOptions options)
    {
        options.RegisterExtension(new DatabaseCapOptionsExtension());
        return options;
    }
}
