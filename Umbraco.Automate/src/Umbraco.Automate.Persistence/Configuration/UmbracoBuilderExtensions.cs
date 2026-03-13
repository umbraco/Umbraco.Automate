using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Messaging;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Versioning;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Persistence.Automations;
using Umbraco.Automate.Persistence.Connections;
using Umbraco.Automate.Persistence.Notifications;
using Umbraco.Automate.Persistence;
using Umbraco.Automate.Persistence.Workspaces;
using Umbraco.Automate.Persistence.Outbox;
using Umbraco.Automate.Persistence.Runs;
using Umbraco.Automate.Persistence.Versioning;
using Umbraco.Automate.Persistence.Workflows;
using Umbraco.Automate.Core.Persistence;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Extensions;
using WorkflowCore.Interface;

namespace Umbraco.Automate.Extensions;

/// <summary>
/// Extension methods for configuring Umbraco Automate persistence.
/// </summary>
public static partial class UmbracoBuilderExtensions
{
    /// <summary>
    /// Adds EF Core persistence for Umbraco Automate.
    /// </summary>
    public static IUmbracoBuilder AddUmbracoAutomatePersistence(this IUmbracoBuilder builder)
    {
        builder.Services.AddUmbracoDbContext<UmbracoAutomateDbContext>((serviceProvider, options, umbracoConnectionString, umbracoProviderName) =>
        {
            // Resolve at context creation time (not registration time) to avoid null
            // connection strings on fresh installs where the DB isn't configured yet.
            var config = serviceProvider.GetRequiredService<IConfiguration>();
            var (dedicatedCs, dedicatedProvider) = DatabaseConnectionInfo.Resolve(config);

            // Prefer dedicated umbracoAutomateDbDSN if configured; fall back to umbracoDbDSN.
            var connectionString = !string.IsNullOrEmpty(dedicatedCs) ? dedicatedCs : umbracoConnectionString;
            var providerName = !string.IsNullOrEmpty(dedicatedCs) ? (dedicatedProvider ?? umbracoProviderName) : umbracoProviderName;

            ConfigureDatabaseProvider(options, connectionString, providerName);
        });

        builder.Services.AddSingleton<AutomationFactory>();
        builder.Services.AddSingleton<ConnectionFactory>();
        builder.Services.AddSingleton<IAutomationRepository, EFCoreAutomationRepository>();
        builder.Services.AddSingleton<IWorkspaceRepository, EFCoreWorkspaceRepository>();
        builder.Services.AddSingleton<IConnectionRepository, EFCoreConnectionRepository>();
        builder.Services.AddSingleton<IAutomationRunRepository, EFCoreAutomationRunRepository>();
        builder.Services.AddSingleton<IPersistenceProvider, EFCoreWorkflowPersistenceProvider>();
        builder.Services.AddSingleton<IOutboxStore, EFCoreOutboxStore>();
        builder.Services.AddSingleton<IEntityVersionRepository, EFCoreEntityVersionRepository>();

        // Run pending EF Core migrations on startup.
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, RunAutomateMigrationNotificationHandler>();

        // Recover runs stuck in Running/Pending from the previous process.
        // Registered after migrations so the schema is up-to-date.
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, StuckRunRecoveryNotificationHandler>();

        return builder;
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
