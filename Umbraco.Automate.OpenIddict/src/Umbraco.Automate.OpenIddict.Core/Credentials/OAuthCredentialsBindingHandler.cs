using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.OpenIddict.ConnectionTypes;

namespace Umbraco.Automate.OpenIddict.Credentials;

/// <summary>
/// Validates the OAuth credential a connection refers to when the connection is saved.
/// </summary>
/// <remarks>
/// <para>
/// The OAuth editor submits the handoff token it received from the callback (see
/// <see cref="OAuthCredentialsHandoffProtector"/>), and this handler exchanges it for the credential id
/// that is stored in the connection's settings. A value equal to the connection's currently stored id
/// is kept as-is, so existing connections and unrelated edits save unchanged.
/// </para>
/// <para>
/// A credential belongs to one connection: a token is refused if another connection already refers to
/// its credential.
/// </para>
/// <para>
/// A raw credential id other than the stored one is refused when a credential with that id exists, and
/// ignored when none does (for example an id carried over from another environment).
/// </para>
/// </remarks>
internal sealed class OAuthCredentialsBindingHandler : IConnectionSettingsSaveHandler
{
    private readonly OAuthCredentialsHandoffProtector _handoffProtector;
    private readonly IOAuthCredentialsService _credentialsService;

    public OAuthCredentialsBindingHandler(
        OAuthCredentialsHandoffProtector handoffProtector,
        IOAuthCredentialsService credentialsService)
    {
        _handoffProtector = handoffProtector;
        _credentialsService = credentialsService;
    }

    public async Task PrepareSettingsAsync(ConnectionSettingsSaveContext context, CancellationToken cancellationToken)
    {
        if (context.ConnectionType is not IOAuthConnectionType oauthType
            || OAuthCredentialReferences.GetCredentialsIdKey(context.ConnectionType) is not { } fieldKey)
        {
            return;
        }

        var provider = oauthType.ProviderName;
        var settings = context.Connection.Settings;

        var keys = settings.Keys.Where(k => string.Equals(k, fieldKey, StringComparison.OrdinalIgnoreCase)).ToList();
        if (keys.Count == 0)
        {
            return;
        }

        if (keys.Count > 1)
        {
            throw NotVerified(provider);
        }

        var key = keys[0];
        var submitted = OAuthCredentialReferences.ReadString(settings[key]);
        if (string.IsNullOrWhiteSpace(submitted))
        {
            // No account connected (or disconnected).
            return;
        }

        var persistedId = GetPersistedCredentialsId(context.PersistedSettings, fieldKey);

        if (Guid.TryParse(submitted, out var submittedId))
        {
            if (submittedId == Guid.Empty || submittedId == persistedId)
            {
                return;
            }

            // A rollback restores a value this connection held in an earlier version. That version may
            // predate these checks, so hold it to the same rules as a newly handed-off credential. Rather
            // than failing the whole rollback, drop a credential that no longer qualifies: the connection
            // is restored as not authenticated, and the user authenticates again.
            if (context.IsRollback)
            {
                if (!await CanBindAsync(context, submittedId, provider, cancellationToken))
                {
                    settings[key] = null;
                }

                return;
            }

            // Environment transfers (such as Umbraco Deploy) copy a connection's settings as they are,
            // including a credential id that only exists in the source environment. An id with no
            // credential behind it grants nothing, so rather than failing the save, ignore it: keep the
            // credential this connection already has, or leave a new connection not authenticated.
            // An id that does resolve to a credential is refused, as it could hand this connection
            // another connection's account.
            if (await _credentialsService.GetCredentialsAsync(submittedId, cancellationToken) is null)
            {
                settings[key] = persistedId is { } currentId && currentId != Guid.Empty
                    ? currentId.ToString()
                    : null;
                return;
            }

            throw NotVerified(provider);
        }

        var handoff = _handoffProtector.Unprotect(submitted);
        if (handoff.Status == OAuthCredentialsHandoffStatus.Invalid
            || !string.Equals(handoff.Provider, provider, StringComparison.OrdinalIgnoreCase))
        {
            throw NotVerified(provider);
        }

        if (handoff.CredentialId == persistedId)
        {
            // The editor re-submitted the token it was given for the credential already stored here.
            settings[key] = handoff.CredentialId.ToString();
            return;
        }

        if (handoff.Status == OAuthCredentialsHandoffStatus.Expired)
        {
            throw new ConnectionSettingsValidationException(
                $"The {provider} authentication has expired. Authenticate with {provider} again, then save.");
        }

        await EnsureCredentialsExistForProviderAsync(handoff.CredentialId, provider, cancellationToken);
        await EnsureNotReferencedElsewhereAsync(context, handoff.CredentialId, provider, cancellationToken);

        settings[key] = handoff.CredentialId.ToString();
    }

    private static Guid? GetPersistedCredentialsId(IReadOnlyDictionary<string, object?>? persisted, string fieldKey)
    {
        if (persisted is null)
        {
            return null;
        }

        var match = persisted.FirstOrDefault(kv => string.Equals(kv.Key, fieldKey, StringComparison.OrdinalIgnoreCase));
        return match.Key is null ? null : OAuthCredentialReferences.ReadGuid(match.Value);
    }

    private async Task EnsureCredentialsExistForProviderAsync(Guid credentialId, string provider, CancellationToken cancellationToken)
    {
        if (!await CredentialsExistForProviderAsync(credentialId, provider, cancellationToken))
        {
            throw NotVerified(provider);
        }
    }

    private async Task<bool> CredentialsExistForProviderAsync(Guid credentialId, string provider, CancellationToken cancellationToken)
    {
        var credentials = await _credentialsService.GetCredentialsAsync(credentialId, cancellationToken);
        return credentials is not null && string.Equals(credentials.Provider, provider, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> CanBindAsync(
        ConnectionSettingsSaveContext context,
        Guid credentialId,
        string provider,
        CancellationToken cancellationToken)
    {
        if (!await CredentialsExistForProviderAsync(credentialId, provider, cancellationToken))
        {
            return false;
        }

        var connections = await context.GetAllConnectionsAsync(cancellationToken);
        return !OAuthCredentialReferences.IsReferencedByOtherConnection(connections, context.Connection.Id, credentialId);
    }

    private static async Task EnsureNotReferencedElsewhereAsync(
        ConnectionSettingsSaveContext context,
        Guid credentialId,
        string provider,
        CancellationToken cancellationToken)
    {
        var connections = await context.GetAllConnectionsAsync(cancellationToken);
        if (OAuthCredentialReferences.IsReferencedByOtherConnection(connections, context.Connection.Id, credentialId))
        {
            throw new ConnectionSettingsValidationException(
                $"This {provider} authentication is already used by another connection. Authenticate with {provider} again, then save.");
        }
    }

    private static ConnectionSettingsValidationException NotVerified(string provider)
        => new($"The {provider} account for this connection could not be verified. Authenticate with {provider} again, then save.");
}
