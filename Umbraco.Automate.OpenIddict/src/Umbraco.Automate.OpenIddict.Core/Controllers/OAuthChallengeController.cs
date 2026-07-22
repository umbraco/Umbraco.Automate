using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Client.AspNetCore;
using Umbraco.Automate.OpenIddict.Extensions;
using Umbraco.Automate.OpenIddict.Providers;
using Umbraco.Cms.Api.Common.Attributes;

namespace Umbraco.Automate.OpenIddict.Controllers;

/// <summary>
/// Initiates an OAuth challenge that redirects the user to the external provider's authorize page.
/// No authorization attribute — the endpoint only redirects to the external provider.
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
    /// Opens in a popup — the callback will close the popup when complete.
    /// </summary>
    /// <param name="provider">The OpenIddict provider name (e.g. "Slack", "GitHub").</param>
    [HttpGet("challenge/{provider}")]
    public IActionResult Challenge(string provider)
    {
        // The OAuth Provider Status endpoint lets the UI warn before this is ever called, but
        // guard here too — OpenIddict throws an unhandled InvalidOperationException rather than
        // a friendly result if the client ID is missing when the challenge is dispatched.
        var configuration = _configurationSource.GetConfiguration(provider);
        if (!configuration.HasClientCredentials())
        {
            return OAuthPopupResult.Failure(
                $"{provider} is not configured. Add a client ID and secret under " +
                $"Umbraco:Automate:Providers:{provider} in appsettings.json.");
        }

        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Action(
                nameof(OAuthCallbackController.Callback),
                "OAuthCallback",
                new { provider }),
        };

        properties.SetString(OpenIddictClientAspNetCoreConstants.Properties.ProviderName, provider);

        return Challenge(properties, OpenIddictClientAspNetCoreDefaults.AuthenticationScheme);
    }
}
