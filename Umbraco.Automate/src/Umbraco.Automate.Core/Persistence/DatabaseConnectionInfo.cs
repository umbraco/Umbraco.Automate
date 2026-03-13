using Microsoft.Extensions.Configuration;
using Umbraco.Extensions;

namespace Umbraco.Automate.Core.Persistence;

/// <summary>
/// Resolves the database connection string and provider name for Automate packages.
/// Requires a dedicated <c>umbracoAutomateDbDSN</c> connection string to be configured.
/// </summary>
public static class DatabaseConnectionInfo
{
    /// <summary>
    /// The connection string name for the dedicated Automate database.
    /// </summary>
    public const string ConnectionStringName = "umbracoAutomateDbDSN";

    /// <summary>
    /// The custom migrations history table name used by Automate's EF Core migrations.
    /// </summary>
    public const string MigrationsHistoryTable = "__UmbracoAutomate_MigrationsHistory";

    /// <summary>
    /// Resolves the connection string and provider name from configuration.
    /// Throws <see cref="InvalidOperationException"/> if not configured.
    /// </summary>
    public static (string ConnectionString, string ProviderName) Resolve(IConfiguration config)
    {
        var connectionString = config.GetUmbracoConnectionString(ConnectionStringName, out var providerName);

        if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(providerName))
        {
            throw new InvalidOperationException(
                $"Umbraco Automate requires a dedicated connection string. " +
                $"Please configure '{ConnectionStringName}' and " +
                $"'{ConnectionStringName}_ProviderName' in your connection strings.");
        }

        return (connectionString, providerName);
    }
}
