namespace Umbraco.Automate.OpenIddict.Credentials;

/// <summary>
/// Repository for OAuth credentials persistence. Internal to the OpenIddict package.
/// </summary>
internal interface IOAuthCredentialsRepository
{
    /// <summary>
    /// Gets credentials by unique ID.
    /// </summary>
    Task<OAuthCredentials?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves credentials (insert or update).
    /// </summary>
    Task<OAuthCredentials> SaveAsync(OAuthCredentials credentials, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes credentials by ID.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
