using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using OpenIddict.Client;
using Swashbuckle.AspNetCore.SwaggerGen;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Extensions;
using Umbraco.Automate.OpenIddict;
using Umbraco.Automate.OpenIddict.Credentials;
using Umbraco.Automate.OpenIddict.Credentials.Persistence;
using Umbraco.Automate.OpenIddict.Providers;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
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

        // Credential handoff: the callback returns a protected token instead of the credential id, and
        // the binding handler exchanges it for the id when the connection is saved.
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<OAuthCredentialsHandoffProtector>();
        builder.Services.AddSingleton<IConnectionSettingsSaveHandler, OAuthCredentialsBindingHandler>();

        // Deletes credentials from abandoned authentications that no connection refers to. Runs on the
        // scheduling server only (Umbraco's default server roles) — see OAuthCredentialsCleanupJob.
        builder.Services.AddHostedService<OAuthCredentialsCleanupJob>();

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
        builder.Services.AddOpenIddictDbContext();

        builder.Services.AddSingleton<OAuthCredentialsFactory>();
        builder.Services.AddSingleton<IOAuthCredentialsRepository, EFCoreOAuthCredentialsRepository>();

        // Run pending migrations on startup.
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, RunOpenIddictMigrationNotificationHandler>();
    }

    /// <summary>
    /// Registers the pooled <see cref="OpenIddictDbContext"/> factory and enlists it in the ambient
    /// Umbraco scope. Split out of <c>AddPersistence</c> so the composition tests exercise this exact
    /// registration instead of a hand-kept copy of it.
    /// </summary>
    internal static IServiceCollection AddOpenIddictDbContext(this IServiceCollection services)
    {
        // See AddAutomatePooledDbContext for how the connection string is resolved, why a connection
        // string added after boot is picked up, and why sharing the ambient connection is decided per
        // call. The enlisted context lets OpenIddict join the ambient Umbraco transaction when its
        // connection string points at the Umbraco CMS database; a second connection would compete with
        // the ambient scope for it and, on SQLite, deadlock on the held write lock.
        services.AddAutomatePooledDbContext<OpenIddictDbContext>(
            OpenIddictPooledDbContextOptions.Configure,
            (_, connection, providerName) =>
            {
                var options = new DbContextOptionsBuilder<OpenIddictDbContext>();
                OpenIddictDbContext.ConfigureProvider(options, connection, providerName);

                return new OpenIddictDbContext(options.Options);
            },
            placeholderWarning:
                "Umbraco Automate OpenIddict has no database connection string configured yet. OAuth " +
                "connections stay inactive until one is configured. A connection string added by the " +
                "installer is picked up straight away; one added any other way needs the application to " +
                "restart.",
            foundMessage:
                "Umbraco Automate OpenIddict found its database connection string and OAuth connections " +
                "are now available.",
            loggerCategory: typeof(OpenIddictPooledDbContextOptions).FullName!);

        return services;
    }
}

/// <summary>
/// Applies the provider setup for the pooled <see cref="OpenIddictDbContext"/> factory.
/// </summary>
/// <remarks>
/// Resolving the connection string, the placeholder fallback for a not-yet-configured site (see
/// <see href="https://github.com/umbraco/Umbraco.Automate/issues/487"/>) and the switch to the real
/// connection string once it appears all live in <c>AddAutomatePooledDbContext</c> and
/// <c>ResolvingPooledDbContextFactory</c> (see
/// <see href="https://github.com/umbraco/Umbraco.Automate/issues/531"/> and
/// <see href="https://github.com/umbraco/Umbraco.Automate/issues/532"/>). This only configures the
/// provider for whichever connection string they hand it.
/// </remarks>
internal static class OpenIddictPooledDbContextOptions
{
    internal static void Configure(
        IServiceProvider serviceProvider,
        DbContextOptionsBuilder options,
        string connectionString,
        string providerName)
        => OpenIddictDbContext.ConfigureProvider(options, connectionString, providerName);
}
