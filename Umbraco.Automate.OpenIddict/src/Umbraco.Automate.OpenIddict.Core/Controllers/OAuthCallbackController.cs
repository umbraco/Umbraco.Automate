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
/// Returns HTML that postMessages back to the parent window (popup flow).
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
        if (!result.Succeeded)
        {
            return Content(BuildPopupHtml(success: false, error: "Authentication failed."), "text/html");
        }

        var accessToken = result.Properties?.GetTokenValue(Tokens.BackchannelAccessToken);
        var refreshToken = result.Properties?.GetTokenValue(Tokens.RefreshToken);
        var expiresAt = result.Properties?.ExpiresUtc;

        if (string.IsNullOrEmpty(accessToken))
        {
            return Content(BuildPopupHtml(success: false, error: "No access token received."), "text/html");
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

        return Content(BuildPopupHtml(success: true, credentialId: saved.Id.ToString()), "text/html");
    }

    private static string BuildPopupHtml(bool success, string? credentialId = null, string? error = null)
    {
        var messagePayload = success
            ? $$"""{ "type": "oauth-complete", "success": true, "credentialId": "{{credentialId}}" }"""
            : $$"""{ "type": "oauth-complete", "success": false, "error": "{{error}}" }""";

        return $$"""
            <!DOCTYPE html>
            <html>
            <head><title>OAuth Complete</title></head>
            <body>
                <p>{{(success ? "Authentication successful. This window will close." : $"Authentication failed: {error}")}}</p>
                <script>
                    if (window.opener) {
                        window.opener.postMessage({{messagePayload}}, window.location.origin);
                    }
                    window.close();
                </script>
            </body>
            </html>
            """;
    }
}
