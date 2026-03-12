using Umbraco.Automate.Core.Notifications;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Scoping;

namespace Umbraco.Automate.Core.Connections;

/// <summary>
/// Default implementation of <see cref="IConnectionService"/>.
/// Publishes lifecycle notifications and delegates to the repository.
/// </summary>
internal sealed class ConnectionService : IConnectionService
{
    private readonly IConnectionRepository _connectionRepository;
    private readonly ConnectionTypeCollection _connectionTypeCollection;
    private readonly ICoreScopeProvider _scopeProvider;
    private readonly IEventMessagesFactory _eventMessagesFactory;

    public ConnectionService(
        IConnectionRepository connectionRepository,
        ConnectionTypeCollection connectionTypeCollection,
        ICoreScopeProvider scopeProvider,
        IEventMessagesFactory eventMessagesFactory)
    {
        _connectionRepository = connectionRepository;
        _connectionTypeCollection = connectionTypeCollection;
        _scopeProvider = scopeProvider;
        _eventMessagesFactory = eventMessagesFactory;
    }

    public Task<Connection?> GetConnectionAsync(Guid id, CancellationToken cancellationToken = default)
        => _connectionRepository.GetAsync(id, cancellationToken);

    public Task<Connection?> GetConnectionByAliasAsync(string alias, CancellationToken cancellationToken = default)
        => _connectionRepository.GetByAliasAsync(alias, cancellationToken);

    public Task<IEnumerable<Connection>> GetAllConnectionsAsync(CancellationToken cancellationToken = default)
        => _connectionRepository.GetAllAsync(cancellationToken);

    public Task<(IEnumerable<Connection> Items, int Total)> GetConnectionsPagedAsync(
        string? filter = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
        => _connectionRepository.GetPagedAsync(filter, skip, take, cancellationToken);

    public async Task<Connection> CreateConnectionAsync(Connection connection, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        if (connection.Id == Guid.Empty)
        {
            connection.Id = Guid.NewGuid();
        }

        using ICoreScope scope = _scopeProvider.CreateCoreScope();

        var eventMessages = _eventMessagesFactory.Get();

        var savingNotification = new ConnectionSavingNotification(connection, eventMessages);
        if (scope.Notifications.PublishCancelable(savingNotification))
        {
            throw new OperationCanceledException("Connection creation was cancelled by a notification handler.");
        }

        var saved = await _connectionRepository.SaveAsync(connection, userId, cancellationToken);

        scope.Notifications.Publish(new ConnectionSavedNotification(saved, eventMessages));
        scope.Complete();

        return saved;
    }

    public async Task<Connection> UpdateConnectionAsync(Connection connection, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        using ICoreScope scope = _scopeProvider.CreateCoreScope();

        var eventMessages = _eventMessagesFactory.Get();

        var savingNotification = new ConnectionSavingNotification(connection, eventMessages);
        if (scope.Notifications.PublishCancelable(savingNotification))
        {
            throw new OperationCanceledException("Connection update was cancelled by a notification handler.");
        }

        var saved = await _connectionRepository.SaveAsync(connection, userId, cancellationToken);

        scope.Notifications.Publish(new ConnectionSavedNotification(saved, eventMessages));
        scope.Complete();

        return saved;
    }

    public async Task<bool> DeleteConnectionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using ICoreScope scope = _scopeProvider.CreateCoreScope();

        var connection = await _connectionRepository.GetAsync(id, cancellationToken);
        if (connection is null)
        {
            return false;
        }

        var eventMessages = _eventMessagesFactory.Get();

        var deletingNotification = new ConnectionDeletingNotification(connection, eventMessages);
        if (scope.Notifications.PublishCancelable(deletingNotification))
        {
            throw new OperationCanceledException("Connection deletion was cancelled by a notification handler.");
        }

        var deleted = await _connectionRepository.DeleteAsync(id, cancellationToken);

        if (deleted)
        {
            scope.Notifications.Publish(new ConnectionDeletedNotification(connection, eventMessages));
        }

        scope.Complete();
        return deleted;
    }

    public async Task<ConfiguredConnection?> GetConfiguredConnectionAsync(Guid connectionId, CancellationToken cancellationToken = default)
    {
        var connection = await _connectionRepository.GetAsync(connectionId, cancellationToken);
        if (connection is null)
        {
            return null;
        }

        var connectionType = _connectionTypeCollection.GetByAlias(connection.Type);
        if (connectionType is null)
        {
            return null;
        }

        var resolvedSettings = connectionType.ResolveSettings(connection.Settings);
        return new ConfiguredConnection(connection, connectionType, resolvedSettings);
    }
}
