namespace Umbraco.Automate.OpenIddict.Credentials;

/// <summary>
/// Service for managing OAuth credentials — tokens obtained via OpenIddict Client flows.
/// </summary>
public interface IOAuthCredentialsService
{
    /// <summary>
    /// Gets credentials by unique ID.
    /// </summary>
    Task<OAuthCredentials?> GetCredentialsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates new credentials from a completed OAuth flow.
    /// </summary>
    Task<OAuthCredentials> CreateCredentialsAsync(OAuthCredentials credentials, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates existing credentials (e.g. after token refresh).
    /// </summary>
    Task UpdateCredentialsAsync(OAuthCredentials credentials, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes credentials (e.g. when disconnecting an OAuth account).
    /// </summary>
    Task DeleteCredentialsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the IDs of credentials that were neither created nor modified (e.g. by a token refresh) at or
    /// after <paramref name="cutoffUtc"/>. Used to find abandoned authentications to clean up; callers must
    /// still exclude credentials that a connection refers to.
    /// </summary>
    /// <remarks>
    /// The default implementation returns no IDs, so a custom implementation that does not override it
    /// never has credentials cleaned up.
    /// </remarks>
    // TODO (V19): Remove the default implementation.
    Task<IReadOnlyList<Guid>> GetCredentialIdsNotModifiedSinceAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Guid>>([]);

    /// <summary>
    /// Gets a valid access token for the credentials, refreshing if expired.
    /// Returns null if the credentials don't exist or the token cannot be refreshed.
    /// </summary>
    Task<string?> GetValidAccessTokenAsync(Guid credentialsId, CancellationToken cancellationToken = default);
}
