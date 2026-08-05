using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenIddict.Client;
using Umbraco.Automate.Core.Persistence;
using Umbraco.Automate.Extensions;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Automate.OpenIddict;
using Umbraco.Automate.OpenIddict.Credentials;
using Umbraco.Automate.OpenIddict.Credentials.Persistence;
using Umbraco.Automate.OpenIddict.Providers;
using Umbraco.Cms.Api.Common.OpenApi;
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

        // OAuth API OpenAPI document — public (no backoffice authentication requirements), matching v17.
        // The challenge/callback endpoints expose no custom model types, so no schema-ID preservation is needed.
        builder.AddBackOfficeOpenApiDocument(Constants.OAuthApi.ApiName, document => document
            .WithTitle(Constants.OAuthApi.ApiTitle)
            .ConfigureOpenApiOptions(options => options.AddDocumentTransformer((openApiDocument, _, _) =>
            {
                openApiDocument.Info.Description = "OAuth endpoints for Umbraco Automate provider connections.";
                openApiDocument.Info.Version = "Latest";
                return Task.CompletedTask;
            })));

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

                // Placeholder — provider-specific callback URIs are added dynamically
                // by OpenIddictClientCredentialsConfigurator, but OpenIddict requires
                // at least one redirection endpoint at configuration time.
                options.SetRedirectionEndpointUris("umbraco/automate/oauth/callback");

                options.AddEphemeralEncryptionKey()
                    .AddEphemeralSigningKey();

                options.UseAspNetCore()
                    .EnableRedirectionEndpointPassthrough();

                options.UseWebProviders();
            });

        return builder;
    }

    private static void AddPersistence(IUmbracoBuilder builder)
    {
        // Resolve the connection string lazily inside the factory (run time), not here at
        // composition time: hosts like Umbraco Cloud / Deploy synthesise the DSN through the
        // ConnectionStrings options pipeline, which has not run yet during AddComposers().
        //
        // shareUmbracoConnection: false — nothing here resolves an IEfCoreScope<OpenIddictDbContext>.
        // Sharing the ambient connection is handled by AmbientDbContextFactory below, which decides
        // per call rather than once at composition time.
        builder.Services.AddUmbracoDbContext<OpenIddictDbContext>(
            (IServiceProvider serviceProvider, DbContextOptionsBuilder options, string? _, string? _) =>
            {
                var (connectionString, providerName) = DatabaseConnectionInfo.Resolve(
                    serviceProvider.GetRequiredService<IOptionsMonitor<ConnectionStrings>>(),
                    serviceProvider.GetRequiredService<IConfiguration>());
                OpenIddictDbContext.ConfigureProvider(options, connectionString, providerName);
            },
            shareUmbracoConnection: false);

        // Same reasoning as Umbraco.Automate's own persistence: this DbContext resolves the same
        // Automate connection string, so when that points at the Umbraco CMS database a second
        // connection would compete with the ambient scope for it — and on SQLite, which permits one
        // writer per database file, deadlock against a caller holding the write lock across the write.
        // See AmbientDbContextFactory.
        builder.Services.EnlistDbContextFactoryInAmbientScope(
            (serviceProvider, connection) =>
            {
                var (_, providerName) = DatabaseConnectionInfo.Resolve(
                    serviceProvider.GetRequiredService<IOptionsMonitor<ConnectionStrings>>(),
                    serviceProvider.GetRequiredService<IConfiguration>());

                var options = new DbContextOptionsBuilder<OpenIddictDbContext>();
                OpenIddictDbContext.ConfigureProvider(options, connection, providerName);

                return new OpenIddictDbContext(options.Options);
            });

        builder.Services.AddSingleton<OAuthCredentialsFactory>();
        builder.Services.AddSingleton<IOAuthCredentialsRepository, EFCoreOAuthCredentialsRepository>();

        // Run pending migrations on startup.
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, RunOpenIddictMigrationNotificationHandler>();
    }
}
