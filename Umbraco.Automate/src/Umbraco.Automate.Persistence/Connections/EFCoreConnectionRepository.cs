using Microsoft.EntityFrameworkCore;
using Umbraco.Automate.Core.Connections;

namespace Umbraco.Automate.Persistence.Connections;

/// <summary>
/// EF Core implementation of <see cref="IConnectionRepository"/>.
/// </summary>
internal sealed class EFCoreConnectionRepository : IConnectionRepository
{
    private readonly IDbContextFactory<UmbracoAutomateDbContext> _dbContextFactory;
    private readonly ConnectionFactory _factory;

    public EFCoreConnectionRepository(
        IDbContextFactory<UmbracoAutomateDbContext> dbContextFactory,
        ConnectionFactory factory)
    {
        _dbContextFactory = dbContextFactory;
        _factory = factory;
    }

    public async Task<Connection?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        ConnectionEntity? entity = await db.Connections
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        return entity is null ? null : _factory.BuildDomain(entity);
    }

    public async Task<Connection?> GetByAliasAsync(string alias, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        ConnectionEntity? entity = await db.Connections
            .FirstOrDefaultAsync(c => c.Alias == alias, cancellationToken);

        return entity is null ? null : _factory.BuildDomain(entity);
    }

    public async Task<IEnumerable<Connection>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entities = await db.Connections
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        return entities.Select(_factory.BuildDomain);
    }

    public async Task<(IEnumerable<Connection> Items, int Total)> GetPagedAsync(
        string? filter = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

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

        return (items.Select(_factory.BuildDomain), total);
    }

    public async Task<Connection> SaveAsync(Connection connection, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

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
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        ConnectionEntity? entity = await db.Connections.FindAsync([id], cancellationToken);
        if (entity is null)
        {
            return false;
        }

        db.Connections.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Connections.AnyAsync(c => c.Id == id, cancellationToken);
    }
}
