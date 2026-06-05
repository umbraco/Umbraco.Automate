using Microsoft.Extensions.Configuration;
using Umbraco.Extensions;

namespace Umbraco.Automate.Core.Persistence;

/// <summary>
/// Resolves the database connection string and provider name for Automate packages.
/// </summary>
/// <remarks>
/// The connection-string name is taken from <c>Umbraco:Automate:UseNamedConnectionString</c>
/// and defaults to <c>umbracoAutomateDbDSN</c>. Pointing the setting at another entry
/// (e.g. <c>umbracoDbDSN</c>) lets Automate share an existing connection — opt-in because
/// the additional traffic from outbox messages, run history and engine tables can affect
/// the named database's performance.
/// </remarks>
public static class DatabaseConnectionInfo
{
    /// <summary>
    /// The default connection string name used when
    /// <see cref="UseNamedConnectionStringConfigKey"/> is not configured.
    /// </summary>
    public const string ConnectionStringName = "umbracoAutomateDbDSN";

    /// <summary>
    /// Configuration key naming the connection string Automate should resolve.
    /// </summary>
    public const string UseNamedConnectionStringConfigKey = "Umbraco:Automate:UseNamedConnectionString";

    /// <summary>
    /// The custom migrations history table name used by Automate's EF Core migrations.
    /// </summary>
    public const string MigrationsHistoryTable = "__UmbracoAutomate_MigrationsHistory";

    /// <summary>
    /// Resolves the connection string and provider name from configuration.
    /// Throws <see cref="InvalidOperationException"/> if the named connection string
    /// is not configured.
    /// </summary>
    public static (string ConnectionString, string ProviderName) Resolve(IConfiguration config)
    {
        var name = config[UseNamedConnectionStringConfigKey];
        if (string.IsNullOrEmpty(name))
        {
            name = ConnectionStringName;
        }

        var connectionString = config.GetUmbracoConnectionString(name, out var providerName);
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException(
                $"Umbraco Automate requires a database connection string named '{name}'. " +
                $"Either configure a '{name}' entry under 'ConnectionStrings', or set " +
                $"'{UseNamedConnectionStringConfigKey}' to the name of an existing connection " +
                $"string to reuse (e.g. 'umbracoDbDSN' to share the Umbraco CMS database).");
        }

        return (connectionString, NormalizeProviderName(providerName));
    }

    private static string NormalizeProviderName(string? providerName)
    {
        // Umbraco Cloud and Azure-configured connection strings use the legacy
        // System.Data.SqlClient provider name. Map it to the modern Microsoft.Data.SqlClient
        // expected by ConfigureProvider.
        if (string.Equals(providerName, "System.Data.SqlClient", StringComparison.OrdinalIgnoreCase))
        {
            return Umbraco.Cms.Core.Constants.ProviderNames.SQLServer;
        }

        return providerName ?? Umbraco.Cms.Core.Constants.ProviderNames.SQLServer;
    }
}
