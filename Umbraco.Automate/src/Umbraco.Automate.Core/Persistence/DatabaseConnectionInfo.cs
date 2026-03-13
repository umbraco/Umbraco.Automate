using Microsoft.Extensions.Configuration;
using Umbraco.Extensions;

namespace Umbraco.Automate.Core.Persistence;

/// <summary>
/// Resolves the database connection string and provider name for Automate packages.
/// Checks for a dedicated <c>umbracoAutomateDbDSN</c> connection string first,
/// then falls back to the main Umbraco connection string (<c>umbracoDbDSN</c>).
/// </summary>
public static class DatabaseConnectionInfo
{
    /// <summary>
    /// The connection string name for the dedicated Automate database.
    /// </summary>
    public const string ConnectionStringName = "umbracoAutomateDbDSN";

    /// <summary>
    /// Resolves the connection string and provider name from configuration.
    /// </summary>
    public static (string? ConnectionString, string? ProviderName) Resolve(IConfiguration config)
    {
        var connectionString = config.GetUmbracoConnectionString(ConnectionStringName, out var providerName);

        if (string.IsNullOrEmpty(connectionString))
        {
            connectionString = config.GetUmbracoConnectionString(out providerName);
        }

        return (connectionString, providerName);
    }
}
