namespace Umbraco.Automate.Core.Connections;

/// <summary>
/// A connection decorated with its resolved type and settings, ready for runtime use.
/// Decorates <see cref="Connection"/> — all connection properties are surfaced directly.
/// </summary>
public sealed class ConfiguredConnection
{
    private readonly Connection _inner;

    internal ConfiguredConnection(Connection connection, IConnectionType connectionType, object? resolvedSettings)
    {
        _inner = connection;
        ConnectionType = connectionType;
        Settings = resolvedSettings;
    }

    /// <summary>Gets the connection ID.</summary>
    public Guid Id => _inner.Id;

    /// <summary>Gets the unique alias.</summary>
    public string Alias => _inner.Alias;

    /// <summary>Gets the display name.</summary>
    public string Name => _inner.Name;

    /// <summary>Gets the connection type alias.</summary>
    public string Type => _inner.Type;

    /// <summary>Gets the version.</summary>
    public int Version => _inner.Version;

    /// <summary>Gets the connection type definition.</summary>
    public IConnectionType ConnectionType { get; }

    /// <summary>Gets the resolved settings (decrypted, config-vars substituted).</summary>
    public object? Settings { get; }

    /// <summary>Gets the resolved settings as the expected type.</summary>
    /// <typeparam name="T">The settings POCO type.</typeparam>
    /// <returns>The typed settings, or a default instance if settings are null or wrong type.</returns>
    public T GetSettings<T>() where T : class, new()
        => Settings as T ?? new T();
}
