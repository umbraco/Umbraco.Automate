using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenIddict.Client;
using Umbraco.Automate.Core.Persistence;
using Umbraco.Automate.OpenIddict.Credentials;
using Umbraco.Automate.OpenIddict.Credentials.Persistence;
using Umbraco.Automate.OpenIddict.Providers;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Persistence.EFCore;
using Umbraco.Extensions;

namespace Umbraco.Automate.OpenIddict.Extensions;

/// <summary>
/// Extension methods for configuring Umbraco Automate OpenIddict services.
/// </summary>
public static class UmbracoBuilderExtensions
{
    /// <summary>
    /// Adds Umbraco Automate OpenIddict services — OAuth credential storage,
    /// OpenIddict Client with WebIntegration, and callback endpoints.
    /// </summary>
    public static IUmbracoBuilder AddAutomateOpenIddict(this IUmbracoBuilder builder)
    {
        // Prevent multiple registrations.
        if (builder.Services.Any(x => x.ServiceType == typeof(IOAuthCredentialsService)))
        {
            return builder;
        }

        // Persistence
        AddPersistence(builder);

        // Services
        builder.Services.AddSingleton<IOAuthProviderConfigurationSource, ConfigurationOAuthProviderConfigurationSource>();
        builder.Services.AddSingleton<IOAuthCredentialsService, OAuthCredentialsService>();

        // Patches provider credentials from IOAuthProviderConfigurationSource at options resolution time,
        // allowing the source to be replaced (e.g. DB-backed) without changing provider packages.
        builder.Services.AddSingleton<IPostConfigureOptions<OpenIddictClientOptions>, OpenIddictClientCredentialsConfigurator>();

        // OpenIddict Client — providers are added by individual provider packages
        // calling builder.Services.AddOpenIddict().AddClient(...) in their own Composers.
        builder.Services.AddOpenIddict()
            .AddClient(options =>
            {
                options.AllowAuthorizationCodeFlow();

                options.UseAspNetCore()
                    .EnableRedirectionEndpointPassthrough();

                options.UseWebProviders();
            });

        return builder;
    }

    private static void AddPersistence(IUmbracoBuilder builder)
    {
        var (connectionString, providerName) = DatabaseConnectionInfo.Resolve(builder.Config);

        builder.Services.AddUmbracoDbContext<OpenIddictDbContext>((_, options, _, _) =>
        {
            ConfigureDatabaseProvider(options, connectionString, providerName);
        });

        builder.Services.AddSingleton<OAuthCredentialsFactory>();
        builder.Services.AddSingleton<IOAuthCredentialsRepository, EFCoreOAuthCredentialsRepository>();

        // Run pending migrations on startup.
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, RunOpenIddictMigrationNotificationHandler>();
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
                    x.MigrationsAssembly("Umbraco.Automate.OpenIddict.Persistence.SqlServer"));
                break;

            case Umbraco.Cms.Core.Constants.ProviderNames.SQLLite:
            case "Microsoft.Data.SQLite":
                options.UseSqlite(connectionString, x =>
                    x.MigrationsAssembly("Umbraco.Automate.OpenIddict.Persistence.Sqlite"));
                break;

            default:
                throw new InvalidOperationException(
                    $"Database provider '{providerName}' is not supported. Supported: SQL Server, SQLite.");
        }
    }
}
