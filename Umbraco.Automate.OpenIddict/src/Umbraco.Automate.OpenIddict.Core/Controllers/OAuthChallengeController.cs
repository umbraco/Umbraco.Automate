using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Client.AspNetCore;
using Umbraco.Automate.OpenIddict.Extensions;
using Umbraco.Automate.OpenIddict.Providers;
using Umbraco.Cms.Api.Common.Attributes;

namespace Umbraco.Automate.OpenIddict.Controllers;

/// <summary>
/// Initiates an OAuth challenge that redirects the user to the external provider's authorize page.
/// No authorization attribute — the endpoint only redirects to the external provider (or, for the
/// same-tab fallback, back to a validated local return URL).
/// Security is enforced by the state parameter validated on the callback.
/// </summary>
[ApiController]
[Route("umbraco/automate/oauth")]
[MapToApi(Constants.OAuthApi.ApiName)]
[ApiExplorerSettings(GroupName = "OAuth")]
public sealed class OAuthChallengeController : ControllerBase
{
    private readonly IOAuthProviderConfigurationSource _configurationSource;

    /// <summary>
    /// Initializes a new instance of the <see cref="OAuthChallengeController"/> class.
    /// </summary>
    public OAuthChallengeController(IOAuthProviderConfigurationSource configurationSource)
    {
        _configurationSource = configurationSource;
    }

    /// <summary>
    /// Initiates an OAuth authorization code flow for the specified provider.
    /// Normally opens in a popup — the callback will close the popup when complete. When the
    /// browser blocks the popup, the property editor navigates the current tab here instead and
    /// passes <paramref name="returnUrl"/> and <paramref name="nonce"/>; the callback then redirects
    /// back to it.
    /// </summary>
    /// <param name="provider">The OpenIddict provider name (e.g. "Slack", "GitHub").</param>
    /// <param name="returnUrl">
    /// Optional same-origin, root-relative backoffice URL to return to after authentication
    /// (same-tab flow). Anything that is not a local URL is rejected to prevent open redirects.
    /// </param>
    /// <param name="nonce">
    /// Per-tab value the editor uses to recognise the result as one it started. Required with
    /// <paramref name="returnUrl"/>.
    /// </param>
    [HttpGet("challenge/{provider}")]
    public IActionResult Challenge(
        string provider,
        [FromQuery] string? returnUrl = null,
        [FromQuery] string? nonce = null)
    {
        // Reject a non-local return URL (open-redirect guard) or a missing/malformed nonce outright,
        // rather than silently falling back to the popup flow, which would strand the user on a
        // page that cannot close itself.
        if (!OAuthReturnUrl.TryCreate(returnUrl, nonce, out var returnTarget))
        {
            return OAuthPopupResult.Failure("The return URL is not valid.");
        }

        // The OAuth Provider Status endpoint lets the UI warn before this is ever called, but
        // guard here too — OpenIddict throws an unhandled InvalidOperationException rather than
        // a friendly result if the client ID is missing when the challenge is dispatched.
        var configuration = _configurationSource.GetConfiguration(provider);
        if (!configuration.HasClientCredentials())
        {
            var error =
                $"{provider} is not configured. Add a client ID and secret under " +
                $"Umbraco:Automate:Providers:{provider} in appsettings.json.";

            return returnTarget is not null
                ? returnTarget.Failure(provider, error)
                : OAuthPopupResult.Failure(error);
        }

        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Action(
                nameof(OAuthCallbackController.Callback),
                "OAuthCallback",
                new { provider }),
        };

        properties.SetString(OpenIddictClientAspNetCoreConstants.Properties.ProviderName, provider);

        // Round-trip the return target inside the state token OpenIddict protects, so the provider
        // (or anyone replaying the callback) cannot substitute a different redirect target.
        returnTarget?.WriteTo(properties);

        return Challenge(properties, OpenIddictClientAspNetCoreDefaults.AuthenticationScheme);
    }
}
