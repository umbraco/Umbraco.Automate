using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Configuration.Models;
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
    /// Resolves the connection string and provider name, first triggering the
    /// <see cref="ConnectionStrings"/> options pipeline so that hosts which synthesise the
    /// connection string at run time get a chance to populate it.
    /// Throws <see cref="InvalidOperationException"/> if the named connection string
    /// is not configured.
    /// </summary>
    /// <remarks>
    /// This is the overload to use from run-time code (DbContext factories, startup
    /// notification handlers). Umbraco Cloud / Umbraco Deploy do not store the connection
    /// string in appsettings; they generate a SQLite/LocalDB DSN at run time via an
    /// <see cref="IConfigureNamedOptions{ConnectionStrings}"/> (Deploy's
    /// <c>ConfigureConnectionStrings</c>) that writes it back into <see cref="IConfiguration"/>.
    /// That configurator only runs when the <see cref="ConnectionStrings"/> options are first
    /// resolved, so resolving via <see cref="IConfiguration"/> alone at composition time
    /// (before the DI container is built) sees an empty connection string and throws.
    /// Calling <see cref="IOptionsMonitor{ConnectionStrings}.Get"/> here forces that pipeline
    /// to run before we read the (now-populated) value back from configuration.
    /// </remarks>
    public static (string ConnectionString, string ProviderName) Resolve(
        IOptionsMonitor<ConnectionStrings> connectionStrings, IConfiguration config)
    {
        var name = ResolveName(config);

        // Force any IConfigureNamedOptions<ConnectionStrings> (e.g. Umbraco Deploy) to run.
        // We rely on its side effect of writing the generated DSN back into IConfiguration,
        // so the return value is intentionally discarded — GetUmbracoConnectionString below
        // handles the appsettings and arbitrary-named-connection cases uniformly.
        _ = connectionStrings.Get(name);

        return ResolveFromConfiguration(name, config);
    }

    /// <summary>
    /// Resolves the connection string and provider name purely from already-materialised
    /// configuration. Throws <see cref="InvalidOperationException"/> if the named connection
    /// string is not configured.
    /// </summary>
    /// <remarks>
    /// This overload does <strong>not</strong> trigger the <see cref="ConnectionStrings"/>
    /// options pipeline, so it must only be used once the connection string is known to be
    /// present in configuration. Run-time callers should prefer the
    /// <see cref="Resolve(IOptionsMonitor{ConnectionStrings}, IConfiguration)"/> overload.
    /// </remarks>
    public static (string ConnectionString, string ProviderName) Resolve(IConfiguration config)
        => ResolveFromConfiguration(ResolveName(config), config);

    private static string ResolveName(IConfiguration config)
    {
        var name = config[UseNamedConnectionStringConfigKey];
        return string.IsNullOrEmpty(name) ? ConnectionStringName : name;
    }

    private static (string ConnectionString, string ProviderName) ResolveFromConfiguration(
        string name, IConfiguration config)
    {
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
