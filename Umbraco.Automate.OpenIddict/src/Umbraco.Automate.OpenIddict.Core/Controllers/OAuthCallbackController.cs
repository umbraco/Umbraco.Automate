using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Client.AspNetCore;
using Umbraco.Automate.OpenIddict.Credentials;
using Umbraco.Cms.Api.Common.Attributes;
using static OpenIddict.Client.AspNetCore.OpenIddictClientAspNetCoreConstants;

namespace Umbraco.Automate.OpenIddict.Controllers;

/// <summary>
/// Handles OAuth callback redirects from external providers.
/// Anonymous — validated via the state token that OpenIddict manages.
/// Returns HTML that postMessages back to the parent window (popup flow), or — when the challenge
/// round-tripped a validated return URL (same-tab fallback for blocked popups) — redirects back to
/// the backoffice with the result in the URL fragment.
/// </summary>
[ApiController]
[Route("umbraco/automate/oauth")]
[MapToApi(Constants.OAuthApi.ApiName)]
[ApiExplorerSettings(GroupName = "OAuth")]
public sealed class OAuthCallbackController : ControllerBase
{
    private readonly IOAuthCredentialsService _credentialsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="OAuthCallbackController"/> class.
    /// </summary>
    public OAuthCallbackController(IOAuthCredentialsService credentialService)
    {
        _credentialsService = credentialService;
    }

    /// <summary>
    /// Processes the OAuth callback — exchanges the authorization code for tokens and stores them.
    /// </summary>
    /// <param name="provider">The OpenIddict provider name.</param>
    [HttpGet("callback/{provider}")]
    [HttpPost("callback/{provider}")]
    public async Task<IActionResult> Callback(string provider)
    {
        var result = await HttpContext.AuthenticateAsync(OpenIddictClientAspNetCoreDefaults.AuthenticationScheme);

        // Present only for the same-tab flow. It was validated by the challenge and protected inside
        // the state token; ReadFrom re-validates it as defence in depth before redirecting anywhere.
        var returnTarget = OAuthReturnUrl.ReadFrom(result.Properties);

        if (!result.Succeeded)
        {
            return Failure("Authentication failed.");
        }

        var accessToken = result.Properties?.GetTokenValue(Tokens.BackchannelAccessToken);
        var refreshToken = result.Properties?.GetTokenValue(Tokens.RefreshToken);
        var expiresAt = result.Properties?.ExpiresUtc;

        if (string.IsNullOrEmpty(accessToken))
        {
            return Failure("No access token received.");
        }

        // The {provider} route segment is the lowercased convention used for the callback URL
        // (see OpenIddictClientCredentialsConfigurator), not the registration's actual ProviderName.
        // Use the original-case value OpenIddict round-tripped through the authentication
        // properties so RefreshAccessTokenAsync's later case-sensitive registration lookup succeeds.
        var resolvedProvider = result.Properties?.GetString(Properties.ProviderName) ?? provider;

        // Extract optional well-known properties that providers may set via event handlers.
        string? userAccessToken = null, accountLabel = null, scopes = null;
        var items = result.Properties?.Items;
        items?.TryGetValue(Constants.OAuthProperties.UserAccessToken, out userAccessToken);
        items?.TryGetValue(Constants.OAuthProperties.AccountLabel, out accountLabel);
        items?.TryGetValue(Constants.OAuthProperties.Scopes, out scopes);

        var credential = new OAuthCredentials
        {
            Provider = resolvedProvider,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            UserAccessToken = !string.IsNullOrEmpty(userAccessToken) ? userAccessToken : null,
            // Only store expiry when a refresh token is available — providers like Slack
            // issue long-lived tokens with no refresh mechanism, so the expiry from
            // OpenIddict's response is misleading and would cause premature rejection.
            ExpiresUtc = !string.IsNullOrEmpty(refreshToken) ? expiresAt?.UtcDateTime : null,
            Scopes = scopes,
            AccountLabel = accountLabel,
        };

        var saved = await _credentialsService.CreateCredentialsAsync(credential);

        return returnTarget is not null
            ? returnTarget.Success(resolvedProvider, saved.Id.ToString())
            : OAuthPopupResult.Success(saved.Id.ToString());

        IActionResult Failure(string error) =>
            returnTarget is not null
                ? returnTarget.Failure(result.Properties?.GetString(Properties.ProviderName) ?? provider, error)
                : OAuthPopupResult.Failure(error);
    }
}
