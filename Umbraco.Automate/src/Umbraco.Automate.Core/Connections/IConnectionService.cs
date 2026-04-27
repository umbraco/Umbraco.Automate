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

    /// <summary>
    /// Gets a connection with its type resolved and settings deserialized, ready for runtime use.
    /// Returns null if the connection or its type is not found.
    /// </summary>
    Task<ConfiguredConnection?> GetConfiguredConnectionAsync(Guid connectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets multiple connections with their types resolved and settings deserialized, ready for runtime use.
    /// Connections whose type is not found are excluded from the result.
    /// </summary>
    Task<IReadOnlyList<ConfiguredConnection>> GetConfiguredConnectionsByIdsAsync(IReadOnlyCollection<Guid> connectionIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls a connection back to a previously saved version.
    /// </summary>
    Task<Connection> RollbackConnectionAsync(
        Guid connectionId,
        int targetVersion,
        Guid? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the connection type's connectivity check against the stored settings.
    /// Returns <c>null</c> if the connection does not exist; returns a
    /// <see cref="ConnectionValidationStatus.Failure"/> result if the connection exists but
    /// its type is no longer registered (e.g. provider package uninstalled).
    /// </summary>
    Task<ConnectionValidationResult?> TestConnectionAsync(Guid connectionId, CancellationToken cancellationToken = default);
}
