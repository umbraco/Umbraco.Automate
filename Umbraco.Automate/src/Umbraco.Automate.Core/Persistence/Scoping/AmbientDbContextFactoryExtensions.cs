using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Umbraco.Automate.Core.Persistence.Scoping;

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
    /// Builds a context bound to the ambient connection. Must configure the same provider and
    /// interceptors as the pooled factory, so a write behaves the same however it is created.
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
        Func<IServiceProvider, DbConnection, TDbContext> createEnlistedContext)
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
                connection => createEnlistedContext(serviceProvider, connection)),
            pooled.Lifetime));

        return services;
    }

    private static IDbContextFactory<TDbContext> ResolvePooledFactory<TDbContext>(
        IServiceProvider serviceProvider,
        ServiceDescriptor pooled)
        where TDbContext : DbContext
    {
        var instance = pooled.ImplementationInstance
            ?? pooled.ImplementationFactory?.Invoke(serviceProvider)
            ?? ActivatorUtilities.CreateInstance(serviceProvider, pooled.ImplementationType!);

        return (IDbContextFactory<TDbContext>)instance;
    }
}
