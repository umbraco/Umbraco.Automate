using DotNetCore.CAP.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Persistence.Automations;
using Umbraco.Automate.Persistence.Notifications;
using Umbraco.Automate.Persistence.Runs;
using Umbraco.Automate.Persistence.Transport;
using Umbraco.Automate.Persistence.Workflows;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Automate.Persistence;
using Umbraco.Extensions;
using WorkflowCore.Interface;

namespace Umbraco.Automate.Extensions;

/// <summary>
/// Extension methods for configuring Umbraco Automate persistence.
/// </summary>
public static partial class UmbracoBuilderExtensions
{
    /// <summary>
    /// The connection string name for the dedicated Automate database.
    /// Follows the Umbraco Commerce convention (<c>umbracoAutomateDbDSN</c>).
    /// When not set, falls back to the main Umbraco connection string (<c>umbracoDbDSN</c>).
    /// </summary>
    internal const string ConnectionStringName = "umbracoAutomateDbDSN";

    /// <summary>
    /// Adds EF Core persistence for Umbraco Automate.
    /// </summary>
    public static IUmbracoBuilder AddUmbracoAutomatePersistence(this IUmbracoBuilder builder)
    {
        // Resolve connection string + provider at registration time from IConfiguration.
        // This is needed because CAP's AddCap() runs its lambda immediately, not at runtime.
        var (connectionString, providerName) = ResolveConnectionInfo(builder.Config);

        builder.Services.AddUmbracoDbContext<UmbracoAutomateDbContext>((_, options, _, _) =>
        {
            ConfigureDatabaseProvider(options, connectionString, providerName);
        });

        builder.Services.AddSingleton<IAutomationRepository, EFCoreAutomationRepository>();
        builder.Services.AddSingleton<IAutomationRunRepository, EFCoreAutomationRunRepository>();
        builder.Services.AddSingleton<IPersistenceProvider, EFCoreWorkflowPersistenceProvider>();

        // CAP — database storage (outbox/inbox) + database transport (shared table)
        builder.Services.AddCap(cap =>
        {
            ConfigureCapStorage(cap, connectionString, providerName);
            cap.UseDatabaseTransport();
        });

        // Override CAP's default storage initializer so tables are named
        // umbracoAutomateOutboxPublished / umbracoAutomateOutboxReceived instead of cap.Published / cap.Received.
        ConfigureCapStorageInitializer(builder.Services, providerName);

        // Run pending EF Core migrations on startup.
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, RunAutomateMigrationNotificationHandler>();

        return builder;
    }

    /// <summary>
    /// Resolves the connection string and provider name from configuration at registration time.
    /// Checks for a dedicated <c>umbracoAutomateDbDSN</c> first, then falls back to <c>umbracoDbDSN</c>.
    /// </summary>
    private static (string? ConnectionString, string? ProviderName) ResolveConnectionInfo(IConfiguration config)
    {
        // Check for dedicated Automate connection string, fall back to Umbraco's.
        // GetUmbracoConnectionString resolves the |DataDirectory| placeholder.
        var connectionString = config.GetUmbracoConnectionString(ConnectionStringName, out var providerName);

        if (string.IsNullOrEmpty(connectionString))
        {
            connectionString = config.GetUmbracoConnectionString(out providerName);
        }

        return (connectionString, providerName);
    }

    private static void ConfigureCapStorage(
        DotNetCore.CAP.CapOptions cap,
        string? connectionString,
        string? providerName)
    {
        if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(providerName))
        {
            // Fallback to in-memory if no connection string (e.g. during design-time migration tooling)
            cap.UseInMemoryStorage();
            return;
        }

        switch (providerName)
        {
            case Umbraco.Cms.Core.Constants.ProviderNames.SQLServer:
                cap.UseSqlServer(connectionString);
                break;

            case Umbraco.Cms.Core.Constants.ProviderNames.SQLLite:
            case "Microsoft.Data.SQLite":
                cap.UseSqlite(connectionString);
                break;

            default:
                cap.UseInMemoryStorage();
                break;
        }
    }

    private static void ConfigureCapStorageInitializer(IServiceCollection services, string? providerName)
    {
        if (string.IsNullOrEmpty(providerName))
        {
            return;
        }

        switch (providerName)
        {
            case Umbraco.Cms.Core.Constants.ProviderNames.SQLServer:
                services.AddSingleton<IStorageInitializer, SqlServerCapStorageInitializer>();
                break;

            case Umbraco.Cms.Core.Constants.ProviderNames.SQLLite:
            case "Microsoft.Data.SQLite":
                services.AddSingleton<IStorageInitializer, SqliteCapStorageInitializer>();
                break;
        }
    }

    private static void ConfigureDatabaseProvider(
        DbContextOptionsBuilder options,
        string? connectionString,
        string? providerName)
    {
        if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(providerName))
        {
            return;
        }

        switch (providerName)
        {
            case Umbraco.Cms.Core.Constants.ProviderNames.SQLServer:
                options.UseSqlServer(connectionString, x =>
                    x.MigrationsAssembly("Umbraco.Automate.Persistence.SqlServer"));
                break;

            case Umbraco.Cms.Core.Constants.ProviderNames.SQLLite:
            case "Microsoft.Data.SQLite":
                options.UseSqlite(connectionString, x =>
                    x.MigrationsAssembly("Umbraco.Automate.Persistence.Sqlite"));
                break;

            default:
                throw new InvalidOperationException(
                    $"Database provider '{providerName}' is not supported. Supported: SQL Server, SQLite.");
        }
    }
}
