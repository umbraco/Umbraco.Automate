using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Persistence;
using Umbraco.Automate.Core.Persistence.Scoping;
using Umbraco.Cms.Core.Configuration.Models;

namespace Umbraco.Automate.Extensions;

/// <summary>
/// Registration helpers for ambient-transaction participation, shared by every Automate product that
/// owns an EF Core DbContext.
/// </summary>
internal static class AmbientDbContextFactoryExtensions
{
    /// <summary>
    /// Wraps the already-registered <see cref="IDbContextFactory{TContext}"/> for
    /// <typeparamref name="TDbContext"/> in an <see cref="AmbientDbContextFactory{TDbContext}"/>, so
    /// every consumer of the factory gets ambient-transaction participation without having to ask for
    /// it. Call after <c>AddUmbracoDbContext&lt;TDbContext&gt;</c>.
    /// </summary>
    /// <typeparam name="TDbContext">The DbContext whose factory should participate.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="createEnlistedContext">
    /// Builds a context bound to the ambient connection, for the resolved provider. Must configure the
    /// same provider and interceptors as the pooled factory, so a write behaves the same however it is
    /// created.
    /// </param>
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
    internal static IServiceCollection EnlistDbContextFactoryInAmbientScope<TDbContext>(
        this IServiceCollection services,
        Func<IServiceProvider, DbConnection, string, TDbContext> createEnlistedContext)
        where TDbContext : DbContext
    {
        // Shared by every participating DbContext: whether Automate targets the Umbraco database, and
        // the ambient transaction if it does, are properties of the host rather than of one context.
        services.TryAddSingleton<IAmbientAutomateConnection, UmbracoAmbientAutomateConnection>();

        ServiceDescriptor pooled = services.LastOrDefault(
                descriptor => descriptor.ServiceType == typeof(IDbContextFactory<TDbContext>))
            ?? throw new InvalidOperationException(
                $"No IDbContextFactory<{typeof(TDbContext).Name}> is registered, so there is nothing " +
                $"to decorate. Call AddUmbracoDbContext<{typeof(TDbContext).Name}>() before " +
                $"{nameof(EnlistDbContextFactoryInAmbientScope)}.");

        services.Remove(pooled);

        // Both registrations keep the lifetime AddUmbracoDbContext chose, rather than assuming the
        // singleton that AddPooledDbContextFactory happens to use today.
        services.Add(ServiceDescriptor.Describe(
            typeof(IDetachedDbContextFactory<TDbContext>),
            serviceProvider => new DetachedDbContextFactory<TDbContext>(
                ResolvePooledFactory<TDbContext>(serviceProvider, pooled)),
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
