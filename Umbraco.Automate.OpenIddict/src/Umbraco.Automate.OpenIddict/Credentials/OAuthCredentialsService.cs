using Microsoft.Extensions.Logging;
using OpenIddict.Client;

namespace Umbraco.Automate.OpenIddict.Credentials;

/// <summary>
/// Default implementation of <see cref="IOAuthCredentialsService"/>.
/// Handles credentials CRUD and transparent token refresh via OpenIddict Client.
/// </summary>
internal sealed class OAuthCredentialsService : IOAuthCredentialsService
{
    /// <summary>
    /// Buffer before token expiry at which a refresh is triggered.
    /// </summary>
    private static readonly TimeSpan ExpiryBuffer = TimeSpan.FromMinutes(5);

    private readonly IOAuthCredentialsRepository _repository;
    private readonly OpenIddictClientService _clientService;
    private readonly ILogger<OAuthCredentialsService> _logger;

    public OAuthCredentialsService(
        IOAuthCredentialsRepository repository,
        OpenIddictClientService clientService,
        ILogger<OAuthCredentialsService> logger)
    {
        _repository = repository;
        _clientService = clientService;
        _logger = logger;
    }

    public Task<OAuthCredentials?> GetCredentialsAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetAsync(id, cancellationToken);

    public async Task<OAuthCredentials> CreateCredentialsAsync(OAuthCredentials credentials, CancellationToken cancellationToken = default)
    {
        if (credentials.Id == Guid.Empty)
        {
            credentials.Id = Guid.NewGuid();
        }

        credentials.DateCreated = DateTime.UtcNow;
        credentials.DateModified = DateTime.UtcNow;

        return await _repository.SaveAsync(credentials, cancellationToken);
    }

    public async Task UpdateCredentialsAsync(OAuthCredentials credentials, CancellationToken cancellationToken = default)
    {
        credentials.DateModified = DateTime.UtcNow;
        await _repository.SaveAsync(credentials, cancellationToken);
    }

    public Task DeleteCredentialsAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.DeleteAsync(id, cancellationToken).AsTask();

    public async Task<string?> GetValidAccessTokenAsync(Guid credentialsId, CancellationToken cancellationToken = default)
    {
        var credentials = await _repository.GetAsync(credentialsId, cancellationToken);
        if (credentials is null)
        {
            return null;
        }

        // Token is still valid (with buffer).
        if (credentials.ExpiresUtc is null || credentials.ExpiresUtc > DateTime.UtcNow.Add(ExpiryBuffer))
        {
            return credentials.AccessToken;
        }

        // Token expired — attempt refresh if we have a refresh token.
        if (string.IsNullOrEmpty(credentials.RefreshToken))
        {
            _logger.LogWarning(
                "OAuth credentials {CredentialsId} for provider {Provider} have expired and no refresh token is available",
                credentialsId, credentials.Provider);
            return null;
        }

        return await RefreshAccessTokenAsync(credentials, cancellationToken);
    }
    private async Task<string?> RefreshAccessTokenAsync(OAuthCredentials credentials, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _clientService.AuthenticateWithRefreshTokenAsync(
                new OpenIddictClientModels.RefreshTokenAuthenticationRequest
                {
                    ProviderName = credentials.Provider,
                    RefreshToken = credentials.RefreshToken,
                    CancellationToken = cancellationToken,
                });

            if (string.IsNullOrEmpty(result.AccessToken))
            {
                _logger.LogWarning(
                    "Token refresh for credentials {CredentialsId} (provider {Provider}) returned no access token",
                    credentials.Id, credentials.Provider);
                return null;
            }

            credentials.AccessToken = result.AccessToken;
            credentials.RefreshToken = result.RefreshToken ?? credentials.RefreshToken;
            credentials.ExpiresUtc = result.AccessTokenExpirationDate?.UtcDateTime;
            credentials.DateModified = DateTime.UtcNow;

            await _repository.SaveAsync(credentials, cancellationToken);

            _logger.LogInformation(
                "Successfully refreshed OAuth token for credentials {CredentialsId} (provider {Provider})",
                credentials.Id, credentials.Provider);

            return credentials.AccessToken;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to refresh OAuth token for credentials {CredentialsId} (provider {Provider})",
                credentials.Id, credentials.Provider);
            return null;
        }
    }
}

file static class BoolTaskExtensions
{
    public static Task AsTask(this Task<bool> task) => task;
}
