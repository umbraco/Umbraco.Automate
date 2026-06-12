using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using OpenIddict.Client;
using Swashbuckle.AspNetCore.SwaggerGen;
using Umbraco.Automate.Core.Persistence;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Automate.OpenIddict;
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

        // OAuth API swagger doc
        builder.Services.Configure<SwaggerGenOptions>(options =>
        {
            if (options.SwaggerGeneratorOptions.SwaggerDocs.ContainsKey(Constants.OAuthApi.ApiName))
                return;

            options.SwaggerDoc(
                Constants.OAuthApi.ApiName,
                new OpenApiInfo
                {
                    Title = Constants.OAuthApi.ApiTitle,
                    Version = "Latest",
                    Description = "OAuth endpoints for Umbraco Automate provider connections.",
                });
        });

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
        builder.Services.AddUmbracoDbContext<OpenIddictDbContext>((serviceProvider, options, _, _) =>
        {
            var (connectionString, providerName) = DatabaseConnectionInfo.Resolve(
                serviceProvider.GetRequiredService<IOptionsMonitor<ConnectionStrings>>(),
                serviceProvider.GetRequiredService<IConfiguration>());
            OpenIddictDbContext.ConfigureProvider(options, connectionString, providerName);
        });

        builder.Services.AddSingleton<OAuthCredentialsFactory>();
        builder.Services.AddSingleton<IOAuthCredentialsRepository, EFCoreOAuthCredentialsRepository>();

        // Run pending migrations on startup.
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, RunOpenIddictMigrationNotificationHandler>();
    }
}
