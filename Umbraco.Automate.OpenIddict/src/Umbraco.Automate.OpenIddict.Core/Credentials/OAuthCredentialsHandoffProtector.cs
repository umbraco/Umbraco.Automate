using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Umbraco.Automate.OpenIddict.Credentials;

/// <summary>
/// Issues and reads the short-lived token that hands a freshly stored <see cref="OAuthCredentials"/>
/// from the OAuth callback to the connection editor, which submits it with the connection. The token is
/// a Data Protection payload carrying the credential id, the provider and an expiry, so it cannot be
/// forged, altered or reused after it expires. Only <see cref="OAuthCredentialsBindingHandler"/> turns
/// it back into a credential id, when the connection is saved.
/// </summary>
internal sealed class OAuthCredentialsHandoffProtector
{
    /// <summary>
    /// The Data Protection purpose. Dedicated to this token so no other protected payload in the
    /// application can be passed off as one.
    /// </summary>
    internal const string Purpose = "Umbraco.Automate.OpenIddict.CredentialsHandoff.v1";

    /// <summary>
    /// How long a token can be submitted with a connection after authenticating.
    /// </summary>
    internal static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    private readonly IDataProtector _protector;
    private readonly TimeProvider _timeProvider;

    public OAuthCredentialsHandoffProtector(IDataProtectionProvider dataProtectionProvider, TimeProvider timeProvider)
    {
        _protector = dataProtectionProvider.CreateProtector(Purpose);
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Creates a token for the given credential, valid for <see cref="Lifetime"/>.
    /// </summary>
    public string Protect(Guid credentialId, string provider)
    {
        var payload = new HandoffPayload(
            credentialId,
            provider,
            _timeProvider.GetUtcNow().Add(Lifetime).ToUnixTimeSeconds());

        return _protector.Protect(JsonSerializer.Serialize(payload));
    }

    /// <summary>
    /// Reads a token. The credential id and provider are returned for an expired token too, so a caller
    /// can recognise a value that is already bound (e.g. an editor re-submitting the token it was given
    /// earlier); only <see cref="OAuthCredentialsHandoffStatus.Valid"/> may bind a credential.
    /// </summary>
    public OAuthCredentialsHandoff Unprotect(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return OAuthCredentialsHandoff.Invalid;
        }

        HandoffPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<HandoffPayload>(_protector.Unprotect(token));
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException)
        {
            return OAuthCredentialsHandoff.Invalid;
        }

        if (payload is null || payload.Id == Guid.Empty || string.IsNullOrEmpty(payload.Provider))
        {
            return OAuthCredentialsHandoff.Invalid;
        }

        var status = _timeProvider.GetUtcNow().ToUnixTimeSeconds() < payload.Exp
            ? OAuthCredentialsHandoffStatus.Valid
            : OAuthCredentialsHandoffStatus.Expired;

        return new OAuthCredentialsHandoff(status, payload.Id, payload.Provider);
    }

    private sealed record HandoffPayload(Guid Id, string Provider, long Exp);
}

/// <summary>
/// The outcome of <see cref="OAuthCredentialsHandoffProtector.Unprotect"/>.
/// </summary>
internal enum OAuthCredentialsHandoffStatus
{
    /// <summary>Not a token this application issued, or it was altered.</summary>
    Invalid,

    /// <summary>Issued by this application, but past its lifetime.</summary>
    Expired,

    /// <summary>Issued by this application and within its lifetime.</summary>
    Valid,
}

/// <summary>
/// A read handoff token.
/// </summary>
internal sealed record OAuthCredentialsHandoff(OAuthCredentialsHandoffStatus Status, Guid CredentialId, string? Provider)
{
    public static OAuthCredentialsHandoff Invalid { get; } = new(OAuthCredentialsHandoffStatus.Invalid, Guid.Empty, null);
}
