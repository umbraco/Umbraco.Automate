using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Persistence;
using Umbraco.Automate.Core.Persistence.Scoping;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Extensions;

namespace Umbraco.Automate.Extensions;

/// <summary>
/// Registration helpers for ambient-transaction participation, shared by every Automate product that
/// owns an EF Core DbContext.
/// </summary>
internal static class AmbientDbContextFactoryExtensions
{
    /// <summary>
    /// Registers the pooled factory for <typeparamref name="TDbContext"/> and enlists it in the ambient
    /// scope, so a connection string that appears after boot is picked up without a restart.
    /// </summary>
    /// <typeparam name="TDbContext">The DbContext to register.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configureProvider">
    /// Applies the provider and interceptors for a resolved connection string and provider name. Used
    /// for the DI pool and, once a connection string appears, for the real pool.
    /// </param>
    /// <param name="createEnlistedContext">
    /// Builds a context bound to the ambient connection, for the resolved provider. Must configure the
    /// same provider and interceptors as the pooled factory, so a write behaves the same however it is
    /// created.
    /// </param>
    /// <param name="placeholderWarning">
    /// Logged, with the exception, when the DI pool falls back to the placeholder.
    /// </param>
    /// <param name="foundMessage">Logged once when a real connection string is picked up.</param>
    /// <param name="loggerCategory">The logger category for both messages.</param>
    /// <remarks>
    /// The connection string is resolved when the pool's options are built, not at composition time:
    /// hosts like Umbraco Cloud / Deploy synthesise it through the ConnectionStrings options pipeline,
    /// which has not run yet during AddComposers(). A genuinely unconfigured connection string falls
    /// back to <see cref="DatabaseConnectionInfo.PlaceholderConnection"/> so the host can still boot
    /// (see #226); <see cref="ResolvingPooledDbContextFactory{TDbContext}"/> then replaces it.
    /// <c>shareUmbracoConnection: false</c> because sharing the ambient connection is decided per call
    /// by <see cref="AmbientDbContextFactory{TDbContext}"/>, not once at composition time.
    /// </remarks>
    internal static IServiceCollection AddAutomatePooledDbContext<TDbContext>(
        this IServiceCollection services,
        Action<IServiceProvider, DbContextOptionsBuilder, string, string> configureProvider,
        Func<IServiceProvider, DbConnection, string, TDbContext> createEnlistedContext,
        string placeholderWarning,
        string foundMessage,
        string loggerCategory)
        where TDbContext : DbContext
    {
        services.TryAddSingleton<PlaceholderConnectionState<TDbContext>>();

        services.AddUmbracoDbContext<TDbContext>(
            (serviceProvider, options, _, _) =>
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

                    // GetService, not GetRequiredService: this catch block exists so a missing
                    // connection string can never crash the host, so logging it must not either.
                    (serviceProvider.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance)
                        .CreateLogger(loggerCategory)
                        .LogWarning(ex, "{Message}", placeholderWarning);

                    serviceProvider.GetRequiredService<PlaceholderConnectionState<TDbContext>>()
                        .MarkBuiltOnPlaceholder();
                }

                configureProvider(serviceProvider, options, connectionString, providerName);
            },
            shareUmbracoConnection: false);

        return services.AddAmbientAwareFactories(
            createEnlistedContext,
            new ResolvingPooledFactorySettings(configureProvider, foundMessage, loggerCategory));
    }

    /// <summary>
    /// Wraps the already-registered <see cref="IDbContextFactory{TContext}"/> for
    /// <typeparamref name="TDbContext"/> in an <see cref="AmbientDbContextFactory{TDbContext}"/>, so
    /// every consumer of the factory gets ambient-transaction participation without having to ask for
    /// it. Private so new callers go through <see cref="AddAutomatePooledDbContext{TDbContext}"/> and get
    /// the late-connection-string pickup.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The decoration is hand-rolled because neither Umbraco nor this repository takes a dependency on
    /// a decoration library, and the registration has to be replaced rather than added: the whole point
    /// is that the pooled factory stops being what callers resolve.
    /// </para>
    /// <para>
    /// The pooled factory stays reachable as <see cref="IDetachedDbContextFactory{TDbContext}"/>, for
    /// the engine internals that must never join a caller's transaction.
    /// </para>
    /// </remarks>
    private static IServiceCollection AddAmbientAwareFactories<TDbContext>(
        this IServiceCollection services,
        Func<IServiceProvider, DbConnection, string, TDbContext> createEnlistedContext,
        ResolvingPooledFactorySettings resolvingSettings)
        where TDbContext : DbContext
    {
        // Shared by every participating DbContext: whether Automate targets the Umbraco database, and
        // the ambient transaction if it does, are properties of the host rather than of one context.
        services.TryAddSingleton<IAmbientAutomateConnection, UmbracoAmbientAutomateConnection>();

        ServiceDescriptor pooled = services.LastOrDefault(
                descriptor => descriptor.ServiceType == typeof(IDbContextFactory<TDbContext>))
            ?? throw new InvalidOperationException(
                $"No IDbContextFactory<{typeof(TDbContext).Name}> is registered, so there is nothing " +
                $"to decorate. Register it with AddUmbracoDbContext<{typeof(TDbContext).Name}>() first " +
                $"(or use {nameof(AddAutomatePooledDbContext)}, which does so itself).");

        services.Remove(pooled);

        // Both registrations keep the lifetime AddUmbracoDbContext chose, rather than assuming the
        // singleton that AddPooledDbContextFactory happens to use today.
        services.Add(ServiceDescriptor.Describe(
            typeof(IDetachedDbContextFactory<TDbContext>),
            serviceProvider => new DetachedDbContextFactory<TDbContext>(
                WrapInResolvingFactory(
                    serviceProvider,
                    ResolvePooledFactory<TDbContext>(serviceProvider, pooled),
                    resolvingSettings)),
            pooled.Lifetime));

        services.Add(ServiceDescriptor.Describe(
            typeof(IDbContextFactory<TDbContext>),
            serviceProvider => new AmbientDbContextFactory<TDbContext>(
                serviceProvider.GetRequiredService<IDetachedDbContextFactory<TDbContext>>(),
                serviceProvider.GetRequiredService<IAmbientAutomateConnection>(),
                connection => createEnlistedContext(
                    serviceProvider,
                    connection,
                    ResolveProviderName(serviceProvider))),
            pooled.Lifetime));

        return services;
    }

    /// <summary>
    /// Resolves the provider name the same way the pooled factory does, so both paths agree on which
    /// provider the context is being configured for.
    /// </summary>
    private static string ResolveProviderName(IServiceProvider serviceProvider)
    {
        var (_, providerName) = DatabaseConnectionInfo.Resolve(
            serviceProvider.GetRequiredService<IOptionsMonitor<ConnectionStrings>>(),
            serviceProvider.GetRequiredService<IConfiguration>());

        return providerName;
    }

    private static IDbContextFactory<TDbContext> WrapInResolvingFactory<TDbContext>(
        IServiceProvider serviceProvider,
        IDbContextFactory<TDbContext> pooledFactory,
        ResolvingPooledFactorySettings settings)
        where TDbContext : DbContext
    {
        // The state is read by the factory per create, after the pool above was resolved: resolving
        // the pool is what runs the options delegate that sets it.
        return new ResolvingPooledDbContextFactory<TDbContext>(
            pooledFactory,
            serviceProvider.GetRequiredService<PlaceholderConnectionState<TDbContext>>(),
            serviceProvider,
            settings);
    }

    private static IDbContextFactory<TDbContext> ResolvePooledFactory<TDbContext>(
        IServiceProvider serviceProvider,
        ServiceDescriptor pooled)
        where TDbContext : DbContext
    {
        // AddPooledDbContextFactory registers an implementation factory, so one of these two produces
        // the pooled factory DI would have handed out. Deliberately no ActivatorUtilities fallback for
        // a plain type registration: that would build a second factory outside DI, with its own pool,
        // which is worse than failing here if EF Core ever changes how it registers.
        var instance = pooled.ImplementationInstance ?? pooled.ImplementationFactory?.Invoke(serviceProvider);

        return instance as IDbContextFactory<TDbContext>
            ?? throw new InvalidOperationException(
                $"The registered IDbContextFactory<{typeof(TDbContext).Name}> could not be resolved " +
                $"from its service descriptor ({pooled.Lifetime}, " +
                $"implementation type '{pooled.ImplementationType?.Name ?? "none"}'), so " +
                $"{nameof(AmbientDbContextFactory<TDbContext>)} has nothing to fall back to. " +
                "AddUmbracoDbContext is expected to register it via an implementation factory.");
    }
}
