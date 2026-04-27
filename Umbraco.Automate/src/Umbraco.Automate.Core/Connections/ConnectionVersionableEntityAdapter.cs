using System.Text.Json;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Versioning;
using static Umbraco.Automate.Core.Versioning.VersionComparer;

namespace Umbraco.Automate.Core.Connections;

/// <summary>
/// Versionable entity adapter for <see cref="Connection"/> entities.
/// Sensitive settings are encrypted via <see cref="IEditableModelSerializer"/> before
/// being persisted into the version snapshot, mirroring the at-rest encryption applied
/// by the connection repository.
/// </summary>
internal sealed class ConnectionVersionableEntityAdapter : VersionableEntityAdapterBase<Connection>
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly IConnectionService _connectionService;
    private readonly ConnectionTypeCollection _connectionTypes;
    private readonly IEditableModelSerializer _settingsSerializer;
    private readonly ILogger<ConnectionVersionableEntityAdapter> _logger;

    public ConnectionVersionableEntityAdapter(
        IConnectionService connectionService,
        ConnectionTypeCollection connectionTypes,
        IEditableModelSerializer settingsSerializer,
        ILogger<ConnectionVersionableEntityAdapter> logger)
    {
        _connectionService = connectionService;
        _connectionTypes = connectionTypes;
        _settingsSerializer = settingsSerializer;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override string CreateSnapshot(Connection entity)
    {
        var snapshot = new ConnectionSnapshot(
            entity.Id,
            entity.Alias,
            entity.Name,
            entity.Type,
            SerializeSettings(entity.Type, entity.Settings),
            entity.Version,
            entity.DateCreated,
            entity.DateModified,
            entity.CreatedByUserId,
            entity.ModifiedByUserId);

        return JsonSerializer.Serialize(snapshot, SerializerOptions);
    }

    /// <inheritdoc />
    protected override Connection? RestoreFromSnapshot(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            var snapshot = JsonSerializer.Deserialize<ConnectionSnapshot>(json, SerializerOptions);
            if (snapshot is null)
            {
                return null;
            }

            return new Connection
            {
                Id = snapshot.Id,
                Alias = snapshot.Alias,
                Name = snapshot.Name,
                Type = snapshot.Type,
                Settings = DeserializeSettings(snapshot.EncryptedSettings),
                Version = snapshot.Version,
                DateCreated = snapshot.DateCreated,
                DateModified = snapshot.DateModified,
                CreatedByUserId = snapshot.CreatedByUserId,
                ModifiedByUserId = snapshot.ModifiedByUserId,
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize Connection snapshot");
            return null;
        }
    }

    /// <inheritdoc />
    protected override IReadOnlyList<ValueChange> CompareVersions(Connection from, Connection to)
    {
        var changes = new List<ValueChange>();

        CompareScalar(changes, "Alias", from.Alias, to.Alias);
        CompareScalar(changes, "Name", from.Name, to.Name);
        CompareScalar(changes, "Type", from.Type, to.Type);

        // Settings are compared with sensitive values masked. Decrypted values stay
        // out of comparison output so the rollback modal cannot leak credentials.
        var schema = _connectionTypes.GetByAlias(to.Type)?.GetSettingsSchema()
            ?? _connectionTypes.GetByAlias(from.Type)?.GetSettingsSchema();
        var sensitiveKeys = schema?.Fields
            .Where(f => f.IsSensitive)
            .Select(f => f.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

        var allKeys = from.Settings.Keys.Union(to.Settings.Keys);
        foreach (var key in allKeys)
        {
            from.Settings.TryGetValue(key, out var oldVal);
            to.Settings.TryGetValue(key, out var newVal);

            if (sensitiveKeys.Contains(key))
            {
                var oldDisplay = oldVal is null ? null : "********";
                var newDisplay = newVal is null ? null : "********";
                if (!Equals(oldVal, newVal))
                {
                    changes.Add(new ValueChange($"Settings.{key}", oldDisplay, newDisplay));
                }
                continue;
            }

            CompareScalar(changes, $"Settings.{key}", oldVal?.ToString(), newVal?.ToString());
        }

        return changes;
    }

    /// <inheritdoc />
    public override Task RollbackAsync(Guid entityId, int version, Guid? userId = null, CancellationToken cancellationToken = default)
        => _connectionService.RollbackConnectionAsync(entityId, version, userId, cancellationToken);

    /// <inheritdoc />
    protected override async Task<Connection?> GetEntityAsync(Guid entityId, CancellationToken cancellationToken)
        => await _connectionService.GetConnectionAsync(entityId, cancellationToken);

    /// <inheritdoc />
    public override Task<IReadOnlyCollection<ProtectedVersion>> GetProtectedVersionsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<ProtectedVersion>>([]);

    private string? SerializeSettings(string typeAlias, Dictionary<string, object?> settings)
    {
        if (settings.Count == 0)
        {
            return null;
        }

        var schema = _connectionTypes.GetByAlias(typeAlias)?.GetSettingsSchema();
        return _settingsSerializer.Serialize(settings, schema);
    }

    private Dictionary<string, object?> DeserializeSettings(string? encrypted)
        => string.IsNullOrEmpty(encrypted)
            ? []
            : _settingsSerializer.Deserialize<Dictionary<string, object?>>(encrypted) ?? [];

    private sealed record ConnectionSnapshot(
        Guid Id,
        string Alias,
        string Name,
        string Type,
        string? EncryptedSettings,
        int Version,
        DateTime DateCreated,
        DateTime DateModified,
        Guid? CreatedByUserId,
        Guid? ModifiedByUserId);
}
