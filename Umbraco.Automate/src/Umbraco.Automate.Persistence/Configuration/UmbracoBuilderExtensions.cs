using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Persistence.Automations;
using Umbraco.Automate.Persistence.Runs;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Automate.Persistence;
using Umbraco.Extensions;

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
        builder.Services.AddUmbracoDbContext<UmbracoAutomateDbContext>((options, connectionString, providerName, serviceProvider) =>
        {
            ConfigureDatabaseProvider(options, connectionString, providerName);
        });

        builder.Services.AddSingleton<IAutomationRepository, EFCoreAutomationRepository>();
        builder.Services.AddSingleton<IAutomationRunRepository, EFCoreAutomationRunRepository>();

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
