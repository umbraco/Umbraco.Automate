using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using OpenIddict.Client;
using Swashbuckle.AspNetCore.SwaggerGen;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.Persistence;
using Umbraco.Automate.Extensions;
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
        // shareUmbracoConnection: false — nothing here resolves an IEfCoreScope<OpenIddictDbContext>.
        // Sharing the ambient connection is handled by AmbientDbContextFactory below, which decides
        // per call rather than once at composition time.
        builder.Services.AddUmbracoDbContext<OpenIddictDbContext>(
            OpenIddictPooledDbContextOptions.Configure,
            shareUmbracoConnection: false);

        // Same reasoning as Umbraco.Automate's own persistence: this DbContext resolves the same
        // Automate connection string, so when that points at the Umbraco CMS database a second
        // connection would compete with the ambient scope for it — and on SQLite, which permits one
        // writer per database file, deadlock against a caller holding the write lock across the write.
        // See AmbientDbContextFactory.
        builder.Services.EnlistDbContextFactoryInAmbientScope(
            (_, connection, providerName) =>
            {
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

/// <summary>
/// The options delegate for the pooled <see cref="OpenIddictDbContext"/> factory, shared between
/// <c>AddPersistence</c> and the tests that guard it.
/// </summary>
/// <remarks>
/// <para>
/// Resolves the connection string lazily inside the factory (run time), not at composition time:
/// hosts like Umbraco Cloud / Deploy synthesise the DSN through the ConnectionStrings options
/// pipeline, which has not run yet during AddComposers().
/// </para>
/// <para>
/// But <c>AddPooledDbContextFactory</c> runs this delegate the moment something first resolves
/// <c>IDbContextFactory&lt;OpenIddictDbContext&gt;</c>, and the generic host resolves that while
/// building every <c>IHostedService</c>'s constructor graph at <c>Host.StartAsync</c>, before
/// Umbraco's runtime level is known. A genuinely unconfigured connection string (a fresh,
/// not-yet-installed site; an ephemeral CI boot serving only <c>swagger.json</c>) must not throw
/// here, so this falls back to <see cref="DatabaseConnectionInfo.PlaceholderConnection"/>, the same
/// way Umbraco.Automate's own persistence does. Nothing reads or writes OAuth credentials below
/// <c>RuntimeLevel.Run</c>, and the migration handler skips that level too. The pooled options are
/// built once per process, though, so the placeholder outlives the CMS's in-process runtime restart
/// after an install: OAuth credentials only work once the application process itself restarts.
/// See <see href="https://github.com/umbraco/Umbraco.Automate/issues/487"/>.
/// </para>
/// </remarks>
internal static class OpenIddictPooledDbContextOptions
{
    internal static void Configure(
        IServiceProvider serviceProvider,
        DbContextOptionsBuilder options,
        string? cmsConnectionString,
        string? cmsProviderName)
    {
        string connectionString;
        string providerName;
        try
        {
            (connectionString, providerName) = DatabaseConnectionInfo.Resolve(
                serviceProvider.GetRequiredService<IOptionsMonitor<ConnectionStrings>>(),
                serviceProvider.GetRequiredService<IConfiguration>());
        }
        catch (InvalidOperationException ex)
        {
            (connectionString, providerName) = DatabaseConnectionInfo.PlaceholderConnection;

            // GetService, not GetRequiredService: this catch block exists so a missing connection
            // string can never crash the host, so logging it must not risk crashing the host either.
            (serviceProvider.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance)
                .CreateLogger(typeof(OpenIddictPooledDbContextOptions))
                .LogWarning(
                    ex,
                    "Umbraco Automate OpenIddict has no database connection string configured yet. OAuth " +
                    "connections will remain unavailable until one is configured and the application " +
                    "process restarts.");
        }

        OpenIddictDbContext.ConfigureProvider(options, connectionString, providerName);
    }
}
