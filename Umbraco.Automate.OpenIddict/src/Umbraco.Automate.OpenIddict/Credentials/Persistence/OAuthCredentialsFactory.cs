using Umbraco.Automate.Core.Security;
using Umbraco.Automate.OpenIddict.Credentials;

namespace Umbraco.Automate.OpenIddict.Credentials.Persistence;

/// <summary>
/// Maps between <see cref="OAuthCredentials"/> domain model and <see cref="OAuthCredentialsEntity"/> EF entity.
/// Encrypts tokens on save and decrypts on load using <see cref="ISensitiveFieldProtector"/>.
/// </summary>
internal sealed class OAuthCredentialsFactory
{
    private readonly ISensitiveFieldProtector _protector;

    public OAuthCredentialsFactory(ISensitiveFieldProtector protector)
    {
        _protector = protector;
    }

    public OAuthCredentials BuildDomain(OAuthCredentialsEntity entity) => new()
    {
        Id = entity.Id,
        Provider = entity.Provider,
        AccessToken = _protector.Unprotect(entity.AccessToken)!,
        RefreshToken = _protector.Unprotect(entity.RefreshToken),
        ExpiresUtc = entity.ExpiresUtc,
        Scopes = entity.Scopes,
        AccountLabel = entity.AccountLabel,
        DateCreated = entity.DateCreated,
        DateModified = entity.DateModified,
    };

    public OAuthCredentialsEntity BuildEntity(OAuthCredentials credentials) => new()
    {
        Id = credentials.Id,
        Provider = credentials.Provider,
        AccessToken = _protector.Protect(credentials.AccessToken)!,
        RefreshToken = _protector.Protect(credentials.RefreshToken),
        ExpiresUtc = credentials.ExpiresUtc,
        Scopes = credentials.Scopes,
        AccountLabel = credentials.AccountLabel,
        DateCreated = credentials.DateCreated,
        DateModified = credentials.DateModified,
    };

    public void UpdateEntity(OAuthCredentialsEntity entity, OAuthCredentials credentials)
    {
        entity.Provider = credentials.Provider;
        entity.AccessToken = _protector.Protect(credentials.AccessToken)!;
        entity.RefreshToken = _protector.Protect(credentials.RefreshToken);
        entity.ExpiresUtc = credentials.ExpiresUtc;
        entity.Scopes = credentials.Scopes;
        entity.AccountLabel = credentials.AccountLabel;
        entity.DateModified = credentials.DateModified;
        // DateCreated intentionally not updated.
    }
}
