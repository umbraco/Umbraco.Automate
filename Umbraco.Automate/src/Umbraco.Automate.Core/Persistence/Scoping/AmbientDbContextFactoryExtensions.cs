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
    /// The decoration is hand-rolled because neither Umbraco nor this repository takes a dependency on
    /// a decoration library, and the registration has to be replaced rather than added: the whole point
    /// is that the pooled factory stops being what callers resolve.
    /// </remarks>
    internal static IServiceCollection EnlistDbContextFactoryInAmbientScope<TDbContext>(
        this IServiceCollection services,
        Func<IServiceProvider, DbConnection, TDbContext> createEnlistedContext)
        where TDbContext : DbContext
    {
        // Shared by every participating DbContext: whether Automate targets the Umbraco database, and
        // the ambient transaction if it does, are properties of the host rather than of one context.
        services.TryAddSingleton<IAmbientAutomateConnection, UmbracoAmbientAutomateConnection>();

        ServiceDescriptor pooled = services.Last(
            descriptor => descriptor.ServiceType == typeof(IDbContextFactory<TDbContext>));

        services.Remove(pooled);

        services.AddSingleton<IDbContextFactory<TDbContext>>(serviceProvider =>
            new AmbientDbContextFactory<TDbContext>(
                ResolvePooledFactory<TDbContext>(serviceProvider, pooled),
                serviceProvider.GetRequiredService<IAmbientAutomateConnection>(),
                connection => createEnlistedContext(serviceProvider, connection)));

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
