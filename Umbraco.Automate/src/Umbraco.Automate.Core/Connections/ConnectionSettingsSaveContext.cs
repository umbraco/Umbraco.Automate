namespace Umbraco.Automate.Core.Connections;

/// <summary>
/// The input to <see cref="IConnectionSettingsSaveHandler.PrepareSettingsAsync"/>.
/// </summary>
internal sealed class ConnectionSettingsSaveContext
{
    private readonly Func<CancellationToken, Task<IEnumerable<Connection>>> _getAllConnections;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionSettingsSaveContext"/> class.
    /// </summary>
    /// <param name="connection">The connection about to be saved. Handlers may change its <see cref="Connection.Settings"/>.</param>
    /// <param name="connectionType">The connection's registered type.</param>
    /// <param name="persistedSettings">The settings currently persisted for this connection, or <c>null</c> when it is new.</param>
    /// <param name="isRollback">Whether the settings are being restored from one of this connection's own earlier versions.</param>
    /// <param name="getAllConnections">Returns every persisted connection.</param>
    public ConnectionSettingsSaveContext(
        Connection connection,
        IConnectionType connectionType,
        IReadOnlyDictionary<string, object?>? persistedSettings,
        bool isRollback,
        Func<CancellationToken, Task<IEnumerable<Connection>>> getAllConnections)
    {
        Connection = connection;
        ConnectionType = connectionType;
        PersistedSettings = persistedSettings;
        IsRollback = isRollback;
        _getAllConnections = getAllConnections;
    }

    /// <summary>
    /// Gets the connection about to be saved. Handlers may change its <see cref="Connection.Settings"/>.
    /// </summary>
    public Connection Connection { get; }

    /// <summary>
    /// Gets the connection's registered type.
    /// </summary>
    public IConnectionType ConnectionType { get; }

    /// <summary>
    /// Gets the settings currently persisted for this connection, or <c>null</c> when it is being created.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? PersistedSettings { get; }

    /// <summary>
    /// Gets a value indicating whether the settings are being restored from one of this connection's own
    /// earlier versions, rather than submitted by an editor.
    /// </summary>
    public bool IsRollback { get; }

    /// <summary>
    /// Gets every persisted connection (including the one being saved, in its persisted state).
    /// </summary>
    public Task<IEnumerable<Connection>> GetAllConnectionsAsync(CancellationToken cancellationToken)
        => _getAllConnections(cancellationToken);
}
