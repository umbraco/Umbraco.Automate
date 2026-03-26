namespace Umbraco.Automate.Core.Connections;

/// <summary>
/// Repository for connection persistence. Internal implementation detail of <c>IConnectionService</c>.
/// </summary>
internal interface IConnectionRepository
{
    /// <summary>
    /// Gets a connection by its unique ID.
    /// </summary>
    Task<Connection?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a connection by its alias.
    /// </summary>
    Task<Connection?> GetByAliasAsync(string alias, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets connections by a collection of IDs.
    /// </summary>
    Task<IEnumerable<Connection>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all connections.
    /// </summary>
    Task<IEnumerable<Connection>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a paged list of connections.
    /// </summary>
    Task<(IEnumerable<Connection> Items, int Total)> GetPagedAsync(
        string? filter = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a connection (insert or update).
    /// </summary>
    Task<Connection> SaveAsync(Connection connection, Guid? userId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a connection by its ID.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a connection with the given ID exists.
    /// </summary>
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}
