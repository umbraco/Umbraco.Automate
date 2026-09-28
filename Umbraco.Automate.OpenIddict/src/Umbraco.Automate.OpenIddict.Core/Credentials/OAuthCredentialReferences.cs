using System.Text.Json;
using Umbraco.Automate.Core.Connections;

namespace Umbraco.Automate.OpenIddict.Credentials;

/// <summary>
/// Works out which <see cref="OAuthCredentials"/> connections refer to. A credential's binding to a
/// connection is the connection's own settings value, so there is no separate binding record to keep in
/// sync.
/// </summary>
internal static class OAuthCredentialReferences
{
    /// <summary>
    /// The settings property name OAuth connection types store the credential id under — the same
    /// convention <c>OAuthConnectionTypeBase</c> reads.
    /// </summary>
    public const string CredentialsIdPropertyName = "OAuthCredentialsId";

    /// <summary>
    /// Returns the settings key for the credential id field of an OAuth connection type's schema, or
    /// <c>null</c> when the schema has no such field.
    /// </summary>
    public static string? GetCredentialsIdKey(IConnectionType connectionType)
        => connectionType.GetSettingsSchema()?.Fields
            .FirstOrDefault(f => f.PropertyName == CredentialsIdPropertyName && f.PropertyType == typeof(Guid?))
            ?.Key;

    /// <summary>
    /// Reads a settings value as a string. Values arrive as <see cref="JsonElement"/> from API requests
    /// and as CLR values from persistence.
    /// </summary>
    public static string? ReadString(object? value) => value switch
    {
        null => null,
        string s => s,
        Guid g => g.ToString(),
        JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
        JsonElement => null,
        _ => value.ToString(),
    };

    /// <summary>
    /// Reads a settings value as a credential id.
    /// </summary>
    public static Guid? ReadGuid(object? value)
        => Guid.TryParse(ReadString(value), out var id) && id != Guid.Empty ? id : null;

    /// <summary>
    /// Returns every id-shaped value in the connections' settings. Deliberately broader than the
    /// credential id field of registered OAuth types: a connection whose type is no longer registered
    /// (provider package removed) still protects its credential from cleanup.
    /// </summary>
    public static HashSet<Guid> CollectReferencedIds(IEnumerable<Connection> connections)
    {
        var ids = new HashSet<Guid>();
        foreach (var connection in connections)
        {
            foreach (var value in connection.Settings.Values)
            {
                if (ReadGuid(value) is { } id)
                {
                    ids.Add(id);
                }
            }
        }

        return ids;
    }

    /// <summary>
    /// Returns whether any connection other than <paramref name="connectionId"/> refers to the credential.
    /// </summary>
    public static bool IsReferencedByOtherConnection(IEnumerable<Connection> connections, Guid connectionId, Guid credentialId)
        => CollectReferencedIds(connections.Where(c => c.Id != connectionId)).Contains(credentialId);

    /// <summary>
    /// Returns the stale credential ids that no connection refers to, i.e. the ones cleanup may delete.
    /// </summary>
    public static IReadOnlyList<Guid> SelectUnreferenced(IEnumerable<Guid> staleCredentialIds, IEnumerable<Connection> connections)
    {
        var referenced = CollectReferencedIds(connections);
        return staleCredentialIds.Where(id => !referenced.Contains(id)).Distinct().ToList();
    }
}
