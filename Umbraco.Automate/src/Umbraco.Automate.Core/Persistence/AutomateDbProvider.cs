using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Umbraco.Automate.Core.Persistence;

/// <summary>
/// Selects and configures the EF Core provider for an Automate DbContext.
/// </summary>
/// <remarks>
/// Shared by every Automate product that owns a DbContext, so which providers are supported, which
/// spellings of their names are accepted, and which migrations history table they use are decided in
/// one place. It also keeps the owned-connection and enlisted-connection paths from drifting apart:
/// the only intended difference between them is retry-on-failure, and having both here makes that
/// visible rather than something to notice across four copies of the same switch.
/// </remarks>
internal static class AutomateDbProvider
{
    /// <summary>
    /// Configures the provider against a connection string the context owns.
    /// </summary>
    internal static void Configure(
        DbContextOptionsBuilder options,
        string connectionString,
        string providerName,
        AutomateMigrationsAssemblies migrationsAssemblies)
    {
        switch (providerName)
        {
            case Umbraco.Cms.Core.Constants.ProviderNames.SQLServer:
                options.UseSqlServer(connectionString, x =>
                {
                    ApplyMigrations(x, migrationsAssemblies.SqlServer);

                    // Safe here, unlike on the enlisted path: this context owns its connection, so
                    // there is no caller-initiated transaction for a retry to have to restart.
                    x.EnableRetryOnFailure();
                });
                break;

            case Umbraco.Cms.Core.Constants.ProviderNames.SQLLite:
            case SqliteAlternateProviderName:
                options.UseSqlite(connectionString, x => ApplyMigrations(x, migrationsAssemblies.Sqlite));
                break;

            default:
                throw Unsupported(providerName);
        }
    }

    /// <summary>
    /// Configures the provider against an already-open connection owned by someone else — the ambient
    /// Umbraco scope — so writes join that connection's transaction.
    /// </summary>
    /// <remarks>
    /// Deliberately does not enable retry-on-failure for SQL Server: EF Core's retrying execution
    /// strategy refuses to run inside a user-initiated transaction, which is exactly what an enlisted
    /// context always has. Retries are the caller's business here, since the caller owns the
    /// transaction that any retry would have to restart.
    /// </remarks>
    internal static void Configure(
        DbContextOptionsBuilder options,
        DbConnection connection,
        string providerName,
        AutomateMigrationsAssemblies migrationsAssemblies)
    {
        switch (providerName)
        {
            case Umbraco.Cms.Core.Constants.ProviderNames.SQLServer:
                options.UseSqlServer(connection, x => ApplyMigrations(x, migrationsAssemblies.SqlServer));
                break;

            case Umbraco.Cms.Core.Constants.ProviderNames.SQLLite:
            case SqliteAlternateProviderName:
                options.UseSqlite(connection, x => ApplyMigrations(x, migrationsAssemblies.Sqlite));
                break;

            default:
                throw Unsupported(providerName);
        }
    }

    // Umbraco's own EFCore extensions accept this spelling alongside the constant, so Automate does too.
    private const string SqliteAlternateProviderName = "Microsoft.Data.SQLite";

    private static void ApplyMigrations<TBuilder, TExtension>(
        RelationalDbContextOptionsBuilder<TBuilder, TExtension> builder,
        string migrationsAssembly)
        where TBuilder : RelationalDbContextOptionsBuilder<TBuilder, TExtension>
        where TExtension : RelationalOptionsExtension, new()
    {
        builder.MigrationsAssembly(migrationsAssembly);
        builder.MigrationsHistoryTable(DatabaseConnectionInfo.MigrationsHistoryTable);
    }

    private static InvalidOperationException Unsupported(string providerName)
        => new($"Database provider '{providerName}' is not supported. Supported: SQL Server, SQLite.");
}
