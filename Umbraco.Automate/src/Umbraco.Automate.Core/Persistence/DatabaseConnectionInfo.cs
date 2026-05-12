using Microsoft.Extensions.Configuration;
using Umbraco.Extensions;

namespace Umbraco.Automate.Core.Persistence;

/// <summary>
/// Resolves the database connection string and provider name for Automate packages.
/// </summary>
/// <remarks>
/// Sharing the CMS database is opt-in to keep the performance impact a conscious choice.
/// Resolution order:
/// <list type="number">
///   <item><description>A dedicated <c>umbracoAutomateDbDSN</c> connection string.</description></item>
///   <item><description>The <c>Umbraco:Automate:UseUmbracoDbDSN</c> flag — falls back to
///     <c>umbracoDbDSN</c>. Intended for hosts (e.g. Umbraco Cloud) where the CMS
///     connection string isn't user-editable so cannot be copied to
///     <c>umbracoAutomateDbDSN</c>.</description></item>
/// </list>
/// Setting both is a configuration error and throws at startup.
/// </remarks>
public static class DatabaseConnectionInfo
{
    /// <summary>
    /// The connection string name for the dedicated Automate database.
    /// </summary>
    public const string ConnectionStringName = "umbracoAutomateDbDSN";

    /// <summary>
    /// Configuration key for the opt-in flag that allows sharing the Umbraco CMS connection.
    /// </summary>
    public const string UseUmbracoDbDSNConfigKey = "Umbraco:Automate:UseUmbracoDbDSN";

    /// <summary>
    /// The custom migrations history table name used by Automate's EF Core migrations.
    /// </summary>
    public const string MigrationsHistoryTable = "__UmbracoAutomate_MigrationsHistory";

    /// <summary>
    /// Resolves the connection string and provider name from configuration.
    /// Throws <see cref="InvalidOperationException"/> if not configured, or if both
    /// <c>umbracoAutomateDbDSN</c> and <c>Umbraco:Automate:UseUmbracoDbDSN</c> are set.
    /// </summary>
    public static (string ConnectionString, string ProviderName) Resolve(IConfiguration config)
    {
        var automateConnectionString = config.GetUmbracoConnectionString(ConnectionStringName, out var automateProviderName);
        var useUmbracoDbDSN = config.GetValue<bool>(UseUmbracoDbDSNConfigKey);

        if (!string.IsNullOrEmpty(automateConnectionString) && useUmbracoDbDSN)
        {
            throw new InvalidOperationException(
                $"Both '{ConnectionStringName}' and '{UseUmbracoDbDSNConfigKey}' are configured. " +
                $"Please configure only one — either set a dedicated '{ConnectionStringName}' " +
                $"connection string, or set '{UseUmbracoDbDSNConfigKey}: true' to share the Umbraco CMS database.");
        }

        if (!string.IsNullOrEmpty(automateConnectionString))
        {
            return (automateConnectionString, NormalizeProviderName(automateProviderName));
        }

        if (useUmbracoDbDSN)
        {
            var umbracoConnectionString = config.GetUmbracoConnectionString(out var umbracoProviderName);
            if (string.IsNullOrEmpty(umbracoConnectionString))
            {
                throw new InvalidOperationException(
                    $"'{UseUmbracoDbDSNConfigKey}' is set but the Umbraco CMS connection string " +
                    $"('umbracoDbDSN') is not configured.");
            }

            return (umbracoConnectionString, NormalizeProviderName(umbracoProviderName));
        }

        throw new InvalidOperationException(
            $"Umbraco Automate requires an explicit database configuration. " +
            $"Either configure a dedicated '{ConnectionStringName}' connection string, " +
            $"or set '{UseUmbracoDbDSNConfigKey}: true' to share the Umbraco CMS database.");
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
