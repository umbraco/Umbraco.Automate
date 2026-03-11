namespace Umbraco.Automate.Core.Connections;

/// <summary>
/// Service for managing connection lifecycle (CRUD).
/// </summary>
public interface IConnectionService
{
    /// <summary>
    /// Gets a connection by its unique ID.
    /// </summary>
    Task<Connection?> GetConnectionAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a connection by its alias.
    /// </summary>
    Task<Connection?> GetConnectionByAliasAsync(string alias, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all connections.
    /// </summary>
    Task<IEnumerable<Connection>> GetAllConnectionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a paged list of connections.
    /// </summary>
    Task<(IEnumerable<Connection> Items, int Total)> GetConnectionsPagedAsync(
        string? filter = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new connection.
    /// </summary>
    Task<Connection> CreateConnectionAsync(Connection connection, Guid? userId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing connection.
    /// </summary>
    Task<Connection> UpdateConnectionAsync(Connection connection, Guid? userId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a connection.
    /// </summary>
    Task<bool> DeleteConnectionAsync(Guid id, CancellationToken cancellationToken = default);
}
