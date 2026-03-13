using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Client.AspNetCore;
using Umbraco.Automate.OpenIddict.Credentials;
using Umbraco.Cms.Api.Common.Attributes;

namespace Umbraco.Automate.OpenIddict.Controllers;

/// <summary>
/// Handles OAuth callback redirects from external providers.
/// Anonymous — validated via the state token that OpenIddict manages.
/// Returns HTML that postMessages back to the parent window (popup flow).
/// </summary>
[ApiController]
[Route("automate/oauth")]
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

        var accessToken = result.Properties?.GetTokenValue("access_token");
        var refreshToken = result.Properties?.GetTokenValue("refresh_token");
        var expiresAt = result.Properties?.ExpiresUtc;

        if (string.IsNullOrEmpty(accessToken))
        {
            return Content(BuildPopupHtml(success: false, error: "No access token received."), "text/html");
        }

        var credential = new OAuthCredentials
        {
            Provider = provider,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresUtc = expiresAt?.UtcDateTime,
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
