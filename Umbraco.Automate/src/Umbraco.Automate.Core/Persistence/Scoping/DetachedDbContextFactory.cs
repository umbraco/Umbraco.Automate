using Microsoft.EntityFrameworkCore;

namespace Umbraco.Automate.Core.Persistence.Scoping;

/// <summary>
/// <see cref="IDetachedDbContextFactory{TDbContext}"/> over the pooled factory that
/// <c>AddUmbracoDbContext</c> registered, kept reachable under its own service type after
/// <see cref="AmbientDbContextFactory{TDbContext}"/> takes over the plain
/// <see cref="IDbContextFactory{TContext}"/> registration.
/// </summary>
/// <typeparam name="TDbContext">The Automate DbContext this factory produces.</typeparam>
internal sealed class DetachedDbContextFactory<TDbContext> : IDetachedDbContextFactory<TDbContext>
    where TDbContext : DbContext
{
    private readonly IDbContextFactory<TDbContext> _pooledFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="DetachedDbContextFactory{TDbContext}"/> class.
    /// </summary>
    /// <param name="pooledFactory">The pooled factory to delegate to.</param>
    public DetachedDbContextFactory(IDbContextFactory<TDbContext> pooledFactory)
        => _pooledFactory = pooledFactory;

    /// <inheritdoc />
    public TDbContext CreateDbContext() => _pooledFactory.CreateDbContext();

    /// <inheritdoc />
    public Task<TDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => _pooledFactory.CreateDbContextAsync(cancellationToken);
}
