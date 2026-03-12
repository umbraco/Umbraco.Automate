using Microsoft.EntityFrameworkCore;
using Umbraco.Automate.Core.Connections;
using Umbraco.Cms.Persistence.EFCore.Scoping;

namespace Umbraco.Automate.Persistence.Connections;

/// <summary>
/// EF Core implementation of <see cref="IConnectionRepository"/>.
/// </summary>
internal sealed class EFCoreConnectionRepository : IConnectionRepository
{
    private readonly IEFCoreScopeProvider<UmbracoAutomateDbContext> _scopeProvider;
    private readonly ConnectionFactory _factory;

    public EFCoreConnectionRepository(
        IEFCoreScopeProvider<UmbracoAutomateDbContext> scopeProvider,
        ConnectionFactory factory)
    {
        _scopeProvider = scopeProvider;
        _factory = factory;
    }

    public async Task<Connection?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        ConnectionEntity? entity = await scope.ExecuteWithContextAsync(async db =>
            await db.Connections.FirstOrDefaultAsync(c => c.Id == id, cancellationToken));

        scope.Complete();
        return entity is null ? null : _factory.BuildDomain(entity);
    }

    public async Task<Connection?> GetByAliasAsync(string alias, CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        ConnectionEntity? entity = await scope.ExecuteWithContextAsync(async db =>
            await db.Connections.FirstOrDefaultAsync(c => c.Alias == alias, cancellationToken));

        scope.Complete();
        return entity is null ? null : _factory.BuildDomain(entity);
    }

    public async Task<IEnumerable<Connection>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        var entities = await scope.ExecuteWithContextAsync(async db =>
            await db.Connections
                .OrderBy(c => c.Name)
                .ToListAsync(cancellationToken));

        scope.Complete();
        return entities.Select(_factory.BuildDomain);
    }

    public async Task<(IEnumerable<Connection> Items, int Total)> GetPagedAsync(
        string? filter = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        var result = await scope.ExecuteWithContextAsync(async db =>
        {
            IQueryable<ConnectionEntity> query = db.Connections;

            if (!string.IsNullOrWhiteSpace(filter))
            {
                query = query.Where(c => c.Name.Contains(filter) || c.Alias.Contains(filter));
            }

            var total = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderBy(c => c.Name)
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);

            return (items, total);
        });

        scope.Complete();
        return (result.items.Select(_factory.BuildDomain), result.total);
    }

    public async Task<Connection> SaveAsync(Connection connection, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        var savedConnection = await scope.ExecuteWithContextAsync(async db =>
        {
            ConnectionEntity? existing = await db.Connections.FindAsync([connection.Id], cancellationToken);

            if (existing is null)
            {
                connection.Version = 1;
                connection.DateModified = DateTime.UtcNow;
                connection.CreatedByUserId = userId;
                connection.ModifiedByUserId = userId;

                ConnectionEntity newEntity = _factory.BuildEntity(connection);
                db.Connections.Add(newEntity);
            }
            else
            {
                connection.Version = existing.Version + 1;
                connection.DateModified = DateTime.UtcNow;
                connection.ModifiedByUserId = userId;

                _factory.UpdateEntity(existing, connection);
            }

            await db.SaveChangesAsync(cancellationToken);
            return connection;
        });

        scope.Complete();
        return savedConnection;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        var deleted = await scope.ExecuteWithContextAsync(async db =>
        {
            ConnectionEntity? entity = await db.Connections.FindAsync([id], cancellationToken);
            if (entity is null)
            {
                return false;
            }

            db.Connections.Remove(entity);
            await db.SaveChangesAsync(cancellationToken);
            return true;
        });

        scope.Complete();
        return deleted;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        var exists = await scope.ExecuteWithContextAsync(async db =>
            await db.Connections.AnyAsync(c => c.Id == id, cancellationToken));

        scope.Complete();
        return exists;
    }
}
