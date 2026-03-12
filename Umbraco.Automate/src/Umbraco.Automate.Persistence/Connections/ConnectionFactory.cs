using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Persistence.Connections;

/// <summary>
/// Maps between <see cref="Connection"/> domain model and <see cref="ConnectionEntity"/> EF entity.
/// Delegates encryption/decryption of sensitive settings to <see cref="IEditableModelSerializer"/>.
/// </summary>
internal sealed class ConnectionFactory
{
    private readonly IEditableModelSerializer _serializer;
    private readonly ConnectionTypeCollection _connectionTypes;

    public ConnectionFactory(
        IEditableModelSerializer serializer,
        ConnectionTypeCollection connectionTypes)
    {
        _serializer = serializer;
        _connectionTypes = connectionTypes;
    }

    public Connection BuildDomain(ConnectionEntity entity)
    {
        Dictionary<string, object?> settings = [];
        if (!string.IsNullOrEmpty(entity.Settings))
        {
            settings = _serializer.Deserialize<Dictionary<string, object?>>(entity.Settings) ?? [];
        }

        return new Connection
        {
            Id = entity.Id,
            Alias = entity.Alias,
            Name = entity.Name,
            Type = entity.Type,
            Settings = settings,
            Version = entity.Version,
            DateCreated = entity.DateCreated,
            DateModified = entity.DateModified,
            CreatedByUserId = entity.CreatedByUserId,
            ModifiedByUserId = entity.ModifiedByUserId,
        };
    }

    public ConnectionEntity BuildEntity(Connection connection)
    {
        return new ConnectionEntity
        {
            Id = connection.Id,
            Alias = connection.Alias,
            Name = connection.Name,
            Type = connection.Type,
            Settings = SerializeSettings(connection.Type, connection.Settings),
            Version = connection.Version,
            DateCreated = connection.DateCreated,
            DateModified = connection.DateModified,
            CreatedByUserId = connection.CreatedByUserId,
            ModifiedByUserId = connection.ModifiedByUserId,
        };
    }

    public void UpdateEntity(ConnectionEntity entity, Connection connection)
    {
        entity.Alias = connection.Alias;
        entity.Name = connection.Name;
        entity.Type = connection.Type;
        entity.Settings = SerializeSettings(connection.Type, connection.Settings);
        entity.Version = connection.Version;
        entity.DateModified = connection.DateModified;
        entity.ModifiedByUserId = connection.ModifiedByUserId;
        // DateCreated and CreatedByUserId intentionally not updated
    }

    /// <summary>
    /// Serializes connection settings to JSON, encrypting sensitive fields based on the
    /// connection type's schema via <see cref="IEditableModelSerializer"/>.
    /// </summary>
    private string? SerializeSettings(string typeAlias, Dictionary<string, object?> settings)
    {
        if (settings.Count == 0)
        {
            return null;
        }

        var schema = _connectionTypes.GetByAlias(typeAlias)?.GetSettingsSchema();
        return _serializer.Serialize(settings, schema);
    }
}
